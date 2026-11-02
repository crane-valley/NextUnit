"""Bounded Linux-only diagnostic; never publishes history or changes GitHub state."""
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import re
import shutil
import signal
import statistics
import subprocess
import tempfile
import time

try:
    import resource
except ImportError:  # Allows fixture validation on Windows; execution is Linux-only.
    resource = None


FRAMEWORKS = ("NEXTUNIT", "TUNIT", "NUNIT", "MSTEST", "XUNIT", "NEXTUNIT-aot", "TUNIT-aot")
SDK = "10.0.403"
RUNTIME = "10.0.14"
DEADLINE = time.monotonic() + 15 * 60


def save(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def command(arguments, cwd, timeout=180):
    remaining = DEADLINE - time.monotonic()
    if remaining <= 0:
        raise RuntimeError("Diagnostic reached its 15-minute internal budget")
    # A new process group lets a timeout stop only this diagnostic's own descendants.
    process = subprocess.Popen(arguments, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, encoding="utf-8", errors="replace", start_new_session=True)
    try:
        out, err = process.communicate(timeout=min(timeout, remaining))
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        out, err = process.communicate()
        raise RuntimeError(f"Timed out: {arguments!r}\n{out}\n{err}") from None
    return process.returncode, out + err


def checked(arguments, cwd, log):
    code, text = command(arguments, cwd)
    log.write_text(text, encoding="utf-8")
    if code:
        raise RuntimeError(f"Command failed ({code}): {arguments!r}; see {log.name}")
    return text.strip()


def cpu():
    fields = Path("/proc/stat").read_text().splitlines()[0].split()
    values = list(map(int, fields[1:9]))  # Exclude guest counters, already in user/nice.
    return values[3] + values[4], sum(values)


def cpu_percent(before, after):
    total = after[1] - before[1]
    return 100 * (1 - (after[0] - before[0]) / total) if total else 0


def child_cpu_ms():
    if resource is None:
        return 0.0
    usage = resource.getrusage(resource.RUSAGE_CHILDREN)
    return 1000 * (usage.ru_utime + usage.ru_stime)


def quiet_check(results, name):
    values = []
    for _ in range(3):
        before = cpu()
        time.sleep(1)
        values.append(cpu_percent(before, cpu()))
    save(results / f"quiet-{name}.json", dict(samples=values, ceiling_percent=10, passed=max(values) <= 10))
    if max(values) > 10:
        raise RuntimeError(f"Inconclusive: host not quiet at {name}: {values}; no automatic retry")


def plan(blocks):
    for block in range(blocks):
        order = ("base", "head", "head", "base") if block % 2 == 0 else ("head", "base", "base", "head")
        for round_index, variant in enumerate(order, 1):
            for position in range(7):
                yield block + 1, round_index, position + 1, variant, FRAMEWORKS[(block + position) % 7]


def binary(binaries, variant, framework, calibration=False):
    source = variant if framework.startswith("NEXTUNIT") and not calibration else "head"
    return binaries / f"{source}-{framework}" / "UnifiedTests"


def build(root, work, results):
    trees = {}
    for variant, variable in (("base", "BASE_SHA"), ("head", "HEAD_SHA")):
        sha = os.environ[variable]
        if not re.fullmatch(r"[0-9a-f]{40}", sha):
            raise ValueError(f"{variable} must be a full lowercase commit SHA")
        resolved = checked(["git", "rev-parse", f"{sha}^{{commit}}"], root, results / f"{variant}-sha.txt")
        if resolved != sha:
            raise ValueError(f"Unexpected resolved {variant} SHA")
        trees[variant] = work / variant
        checked(["git", "worktree", "add", "--detach", str(trees[variant]), sha], root, results / f"checkout-{variant}.log")
        global_json = trees[variant] / "global.json"
        config = json.loads(global_json.read_text())
        config["sdk"] = {"version": SDK, "rollForward": "disable", "allowPrerelease": False}
        save(global_json, config)  # Same SDK-only change in both disposable worktrees.
        version = checked(["dotnet", "--version"], trees[variant], results / f"sdk-{variant}.txt")
        if version != SDK:
            raise RuntimeError(f"Unexpected SDK: {version}")
    runtimes = checked(["dotnet", "--list-runtimes"], root, results / "runtimes.txt")
    if not re.search(rf"^Microsoft.NETCore.App {re.escape(RUNTIME)} \[", runtimes, re.M):
        raise RuntimeError(f"Required runtime {RUNTIME} is not installed")
    binaries = work / "binaries"
    jobs = [("base", "NEXTUNIT"), ("head", "NEXTUNIT")]
    jobs += [("head", f) for f in FRAMEWORKS if f != "NEXTUNIT"]
    jobs.insert(6, ("base", "NEXTUNIT-aot"))
    manifest = {}
    for variant, framework in jobs:
        aot = framework.endswith("-aot")
        identifier = framework.removesuffix("-aot")
        tree = trees[variant]
        project = tree / "tools/speed-comparison/UnifiedTests/UnifiedTests.csproj"
        args = ["dotnet", "publish" if aot else "build", str(project), "-c", "Release", "-f", "net10.0",
                f"-p:TestFramework={identifier}", "--disable-build-servers", "-p:UseSharedCompilation=false",
                "-m:1", "-v:minimal"]
        if aot:
            args += ["-p:Aot=true", "-r", "linux-x64", f"-p:RuntimeFrameworkVersion={RUNTIME}"]
        print(f"Build {variant}-{framework}", flush=True)
        checked(args, tree, results / f"build-{variant}-{framework}.log")
        source = project.parent / f"bin/Release-{identifier}/net10.0"
        if aot:
            source /= "linux-x64/publish"
        destination = binaries / f"{variant}-{framework}"
        shutil.copytree(source, destination)
        if not aot:
            runtime_config = destination / "UnifiedTests.runtimeconfig.json"
            config = json.loads(runtime_config.read_text())
            config["runtimeOptions"]["framework"]["version"] = RUNTIME
            config["runtimeOptions"]["rollForward"] = "Disable"
            save(runtime_config, config)  # Pin the same installed patch for every managed participant.
            shutil.copy2(runtime_config, results / f"runtimeconfig-{variant}-{framework}.json")
            shutil.copy2(destination / "UnifiedTests.deps.json", results / f"deps-{variant}-{framework}.json")
        assets = json.loads((project.parent / "obj/project.assets.json").read_text())
        save(results / f"dependencies-{variant}-{framework}.json",
             {name: {"type": value["type"], "sha512": value.get("sha512")} for name, value in assets["libraries"].items()})
        for file in sorted(destination.rglob("*")):
            if file.is_file():
                manifest[str(file.relative_to(binaries))] = hashlib.sha256(file.read_bytes()).hexdigest()
    save(results / "binary-sha256.json", manifest)
    return binaries


def measure(binaries, results, blocks, calibration):
    results.mkdir()
    outputs = results / "process-output"
    outputs.mkdir()
    save(results / "metadata.json", dict(Blocks=blocks, WarmupPasses=5, Manifest="aa-head" if calibration else "original",
                                        SDK=SDK, Runtime=RUNTIME, ExpectedTestCount=127))
    counter = 0
    rows = []
    with (results / "samples.jsonl").open("w") as samples, (results / "warmup.jsonl").open("w") as warmups:
        def one(variant, framework, block, round_index, position, warmup):
            nonlocal counter
            executable = binary(binaries, variant, framework, calibration)
            before = cpu()
            child_before = child_cpu_ms()
            start_utc = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            start = time.perf_counter()
            code, text = command([str(executable), "--no-progress", "--no-ansi"], executable.parent, timeout=60)
            valid = code == 0 and all(re.search(rf"\b{k}:\s*{v}\b", text, re.I)
                                       for k, v in (("total", 127), ("succeeded", 127), ("failed", 0), ("skipped", 0)))
            elapsed = 1000 * (time.perf_counter() - start)
            occupancy = cpu_percent(before, cpu())
            counter += 1
            filename = f"{counter:05}-{variant}-{framework}.txt"
            (outputs / filename).write_text(text, encoding="utf-8")
            row = dict(StartedUtc=start_utc, Variant=variant, Framework=framework, Executable=str(executable),
                       Block=block, Round=round_index, Position=position, Warmup=warmup,
                       ElapsedMilliseconds=elapsed, ChildCpuMilliseconds=child_cpu_ms() - child_before, SystemCpuPercent=occupancy,
                       ExitCode=code, Valid=valid, OutputFile=filename)
            stream = warmups if warmup else samples
            stream.write(json.dumps(row) + "\n")
            stream.flush()
            if not valid:
                raise RuntimeError(f"Invalid test result; see {filename}")
            if not warmup:
                rows.append(row)
        for pass_index in range(1, 6):
            for framework in FRAMEWORKS:
                one("base", framework, 0, pass_index, 0, True)
                if framework.startswith("NEXTUNIT"):
                    one("head", framework, 0, pass_index, 0, True)
        quiet_check(results, "before-measurement")
        for block, round_index, position, variant, framework in plan(blocks):
            one(variant, framework, block, round_index, position, False)
            if round_index == 4 and position == 7:
                print(f"{results.name}: block {block}/{blocks}", flush=True)
    summarize(rows, results)
    quiet_check(results, "after-measurement")
    (results / "completed.txt").write_text(time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))


def summarize(rows, results):
    rounds = {}
    for row in rows:
        rounds.setdefault((row["Block"], row["Round"]), {})[row["Framework"]] = row
    for entries in rounds.values():
        reference = math.exp(statistics.mean(math.log(entries[f]["ElapsedMilliseconds"])
                                             for f in ("TUNIT", "NUNIT", "MSTEST", "XUNIT")))
        for framework, row in entries.items():
            denominator = entries["TUNIT-aot"]["ElapsedMilliseconds"] if framework.endswith("-aot") else reference
            row["Normalized"] = row["ElapsedMilliseconds"] / denominator
    summary = {}
    for framework in FRAMEWORKS:
        groups = {v: [r for r in rows if r["Variant"] == v and r["Framework"] == framework] for v in ("base", "head")}
        summary[framework] = {}
        for metric in ("ElapsedMilliseconds", "Normalized"):
            values = {v: [r[metric] for r in group] for v, group in groups.items()}
            median = {v: statistics.median(xs) for v, xs in values.items()}
            summary[framework][metric] = dict(medians=median, change_percent=100 * (median["head"] / median["base"] - 1),
                                             samples=values)
    save(results / "summary.json", summary)


def main():
    root = Path(__file__).resolve().parents[2]
    results = root / "same-runner-results"
    results.mkdir()  # Refuse accidental reuse of a previous result directory.
    status = {"status": "incomplete", "base": os.environ.get("BASE_SHA"), "head": os.environ.get("HEAD_SHA")}
    try:
        if platform.system() != "Linux" or platform.machine() != "x86_64":
            raise RuntimeError("This diagnostic only runs on Linux x64")
        save(results / "environment.json", dict(OS=platform.platform(), Python=platform.python_version(), CPUCount=os.cpu_count(),
                                               CPUInfo=Path("/proc/cpuinfo").read_text(), ImageOS=os.environ.get("ImageOS"),
                                               ImageVersion=os.environ.get("ImageVersion"), Base=status["base"], Head=status["head"]))
        work = Path(tempfile.mkdtemp(prefix="same-runner-", dir=os.environ["RUNNER_TEMP"]))
        binaries = build(root, work, results)
        measure(binaries, results / "original", 21, False)
        measure(binaries, results / "aa-head", 7, True)
        status["status"] = "measured; manual review required; no performance gate or history publication"
    except Exception as error:
        status["error"] = str(error)
        raise
    finally:
        save(results / "status.json", status)


if __name__ == "__main__":
    main()
