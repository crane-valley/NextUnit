# Performance regression gate

The round-robin comparison measures NextUnit against TUnit, NUnit, MSTest, and xUnit on every weekly run
and on every pull request that touches `src/` or the tool itself. This document describes what turns those
measurements into a decision: where the history is stored, what counts as a regression, and what a
maintainer does when the gate fires.

The gate is deliberately hard to trip. A benchmark that fails the build on a single unlucky median gets
disabled within a month, so the design gives up detection speed to keep every failure worth reading.

## The metric

Wall-clock milliseconds are not comparable across weeks. A hosted runner varies by tens of percent between
images, neighbours, and CPU models, which swamps any framework change worth finding.

Every participant runs once per round on the same machine within a couple of seconds of the others, so the
gate compares participants rather than clocks. For each round, a participant's sample is divided by the
geometric mean of the reference participants measured in that same round:

```text
normalized(p, round) = elapsed(p, round) / geomean(elapsed(q, round) for q in references)
```

The references are every participant that is not NextUnit's. They are pinned dependencies, so they do not
change when this repository changes, and a machine that is uniformly slow moves the numerator and the
denominator together and cancels out. The geometric mean is the right average here because the result is a
ratio; an arithmetic mean would let the slowest reference dominate.

Only NextUnit's own participants are judged. The competing frameworks are measured, recorded, and reported,
but nothing about them can fail this repository's build.

## The rolling history

Recorded runs live on an orphan `benchmark-data` branch, in `history/round-robin-runtime.jsonl`, one JSON
object per line.

A branch rather than the alternatives: workflow artifacts expire, and a run older than the retention window
is simply gone; `actions/cache` entries are evicted after a week of disuse and whenever the repository
exceeds its cache budget, so a baseline could silently empty itself. A branch survives runner churn, is
reviewable in a pull request, and can be inspected with `git show`. JSON Lines rather than one document:
appending is a single line, a corrupt line damages one run instead of the file, and two runs are diffable
without a tool.

Each line records everything needed to interpret or re-analyse the run later:

| Field | Purpose |
| ----- | ------- |
| `SchemaVersion` | A reader skips lines written by another version rather than failing the gate |
| `GeneratedAtUtc`, `RunId`, `Commit`, `Reference`, `Trigger` | Which run this was, and what produced it |
| `Environment` | Runner image and image build, OS, architecture, processor, processor count, SDK version, runtime version |
| `Rounds`, `ExpectedTestCount` | The workload the samples came from |
| `Participants[].Version` | The framework version each participant was measured at |
| `Participants[].SamplesMilliseconds` | Every raw sample, in round order, so the metric can be recomputed |
| `Participants[].Verdict` | What this run concluded, which is what lets a later run require a repeat |

The file keeps the most recent 100 runs. At the weekly cadence that is about two years, and it stays far
above the 20-run baseline window even if a second runner image is added later.

## Which runs are comparable

A run is compared only against recorded runs with the same baseline key:

```text
round-robin-runtime | ubuntu24 | X64 | sdk 10.0 | runtime 10.0 | references MSTest, NUnit, TUnit, xUnit
```

The key holds the benchmark, the runner image family, the architecture, the SDK and runtime at
**major.minor**, and the set of reference participants.

Patch versions are deliberately excluded. Hosted images move the SDK and runtime patch almost every week,
and keying on them would reset the baseline before it could ever arm, while the metric already cancels the
machine differences that a patch bump might bring. The exact SDK version, runtime version, and image build
are still recorded on every line, so a suspicious result can be traced to them.

The reference set is part of the key because it is the denominator of the metric: dropping or adding a
competing framework changes what every number means, and the older runs must stop being treated as
comparable. Adding or removing one of NextUnit's own participants does not split the baseline.

## What counts as a regression

A run has to clear three independent bars before it is even flagged.

| Bar | Threshold | Why |
| --- | --------- | --- |
| Effect size | at least 5% slower than the baseline median | The workload is startup-heavy and measured end to end. Below this, a change is not distinguishable from ordinary variation in process start cost. |
| Robust spread | at least 3 robust standard deviations above the baseline median | Measured across baseline **run medians**, which are independent observations, so the bar reflects the run-to-run movement this configuration actually exhibits. A noisy baseline raises its own bar. |
| Rank test | one-sided Mann-Whitney U at p < 0.01 | Distribution-free, so it does not assume normally distributed timings, and robust to the occasional outlier a process benchmark always produces. |

The spread bar and the rank test look at different things on purpose. The spread bar asks whether the run
sits outside normal run-to-run movement, using one median per recorded run. The rank test asks whether the
shift is consistent, using the pooled per-round samples of the baseline window. Samples within a run are
correlated, which flatters the rank test, so its significance level is stricter than the customary 0.05 and
it never decides anything on its own.

The baseline is the most recent 20 comparable runs, and the gate stays disarmed below 3. Two runs cannot
show a spread, so a gate armed on them would be guessing at what counts as noise.

## Repetition, and why a pull request never fails on performance

Clearing all three bars produces **Suspected**, which reports and passes. The build fails only at
**Confirmed**, which additionally requires that the recorded run before it was also flagged.

This falls out of one rule rather than a special case. Only default-branch runs append to the history, so
only they can have a predecessor in their own series, and a pull-request run therefore cannot reach
Confirmed. That is the intended behaviour twice over:

- Pull-request runs deliberately do not write history. A pull request measures unmerged code, so appending
  it would contaminate the baseline that the default branch is judged against, and two pushes to the same
  branch would count as a "repeat" of a regression the default branch never had.
- A pull-request run reads the baseline and reports into the job summary and a pull-request comment. A
  suspected regression there is a prompt to look, not a blocked merge.

At the weekly cadence a real regression therefore turns the scheduled run red two weeks after it lands.
A maintainer who does not want to wait can trigger the workflow manually: `workflow_dispatch` runs on the
default branch append to the history like scheduled runs, so a second dispatch confirms immediately.

## Verdicts

| Verdict | Meaning | Effect |
| ------- | ------- | ------ |
| `InsufficientBaseline` | Fewer than 3 comparable runs recorded | Reports, passes |
| `Stable` | Within the thresholds | Reports, passes |
| `Improved` | Faster by the same three bars, mirrored | Reports, passes |
| `Suspected` | Cleared all three bars; no earlier recorded run did | Reports, passes |
| `Confirmed` | Cleared all three bars, and so did the recorded run before it | **Fails the job** |

## Running it locally

The gate consumes the result file the round-robin has just written, so run them in order:

```bash
dotnet run -c Release --project tools/speed-comparison/Tests.Benchmark -- --round-robin 21

dotnet run -c Release --project tools/speed-comparison/Tests.Benchmark -- \
  --analyze-regression --history /path/to/round-robin-runtime.jsonl --series baseline
```

`--analyze-regression` writes `results/REGRESSION_REPORT.md` and `results/history-record.json`, prints the
report, and exits non-zero only on a confirmed regression. Omitting `--history` analyses against an empty
baseline, which is useful for seeing the report format. `--series pull-request` reproduces what a pull
request would report.

To grow a local history, append each record after analysing it:

```bash
dotnet run -c Release --project tools/speed-comparison/Tests.Benchmark -- \
  --append-history --history /path/to/round-robin-runtime.jsonl \
  --record tools/speed-comparison/results/history-record.json
```

The decision logic is covered by `SpeedComparison.Analysis.Tests`, which runs as part of
`dotnet test --solution NextUnit.slnx`.

## Operating the gate

**A confirmed regression that is real.** Fix it. The scheduled run stays red while the regression persists,
because the run before it keeps corroborating.

**A confirmed regression that is an accepted cost.** The history is a git branch, so accepting a new
performance level means retiring the baseline that predates it: truncate
`history/round-robin-runtime.jsonl` on `benchmark-data`, or delete the branch. The next three scheduled
runs rebuild a baseline at the new level, and the gate arms again on top of it. Left alone, the rolling
window absorbs the change eventually, but deliberately retiring the baseline is faster and leaves a
reviewable commit explaining the decision.

**The gate never arms.** Check the baseline key printed in the report. If it changes on every run, a
dimension of the key is churning; the reference set or the runner image family is the usual cause.

**The history branch is not being written.** The `publish-history` job runs only for non-pull-request events
on `main`, and needs `benchmark-data` to be unprotected so `GITHUB_TOKEN` can push to it.

## Workflow shape

One workflow, `speed-comparison.yml`, keeps its existing weekly schedule and path-filtered pull-request run.
No second scheduled comparison was added.

- `benchmark` fetches the history, measures, analyses, reports, and fails on a confirmed regression.
  It runs with `contents: read`.
- `publish-history` appends the run to `benchmark-data`. It is a separate job so that the token able to
  write to a branch is never held by a job that builds and runs pull-request code. It retries on a rejected
  push by refetching and reapplying its record, and a workflow-level concurrency group keyed on the ref
  stops two default-branch runs from racing in the first place.
- `summary` publishes both reports to the job summary.
