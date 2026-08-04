using NextUnit;

namespace SpeedComparison.Analysis.Tests;

/// <summary>Covers the durability guarantees of the rolling history file.</summary>
public class PerformanceHistoryStoreTests
{
    [Test]
    public void MissingFile_ReadsAsAnEmptyHistory()
    {
        using var directory = new TemporaryDirectory();

        var records = PerformanceHistoryStore.Read(directory.Resolve("absent.jsonl"), out var skipped);

        Assert.Equal(0, records.Count);
        Assert.Equal(0, skipped);
    }

    [Test]
    public void AppendedRecordRoundTrips()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");
        var record = SyntheticRuns.Record(
            SyntheticRuns.Samples(seed: 1),
            runId: "run-1",
            generatedAtUtc: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            verdict: RegressionVerdict.Suspected);

        PerformanceHistoryStore.Append(path, record);
        var records = PerformanceHistoryStore.Read(path, out _);

        Assert.Equal(1, records.Count);
        Assert.Equal("run-1", records[0].RunId);
        Assert.Equal(record.GeneratedAtUtc, records[0].GeneratedAtUtc);
        Assert.Equal("ubuntu24", records[0].Environment.RunnerImage);
        var subject = records[0].Participants.Single(participant => participant.Framework == SyntheticRuns.Subject);
        Assert.Equal(RegressionVerdict.Suspected, subject.Verdict);
        Assert.Equal(21, subject.SamplesMilliseconds.Count);
    }

    [Test]
    public void EachRecordOccupiesExactlyOneLine()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");

        for (var index = 0; index < 3; index++)
        {
            PerformanceHistoryStore.Append(path, Record(index));
        }

        Assert.Equal(3, File.ReadAllLines(path).Length);
    }

    [Test]
    public void HistoryIsTrimmedToTheConfiguredWindow()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");

        for (var index = 0; index < PerformanceHistoryStore.MaximumRecords + 5; index++)
        {
            PerformanceHistoryStore.Append(path, Record(index));
        }

        var records = PerformanceHistoryStore.Read(path, out _);

        Assert.Equal(PerformanceHistoryStore.MaximumRecords, records.Count);
        // Trimming drops the oldest runs, so the newest one has to survive.
        Assert.Equal($"run-{PerformanceHistoryStore.MaximumRecords + 4}", records[^1].RunId);
    }

    [Test]
    public void AppendingTheSameRunTwiceRecordsItOnce()
    {
        // A publishing job that retries after an uncertain push must not turn one measurement into two
        // independent baseline observations.
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");

        PerformanceHistoryStore.Append(path, Record(1));
        PerformanceHistoryStore.Append(path, Record(1));

        var records = PerformanceHistoryStore.Read(path, out _);

        Assert.Equal(1, records.Count);
        Assert.Equal("run-1", records[0].RunId);
    }

    [Test]
    public void ReappendingTheSameRunReplacesTheStoredVerdict()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");
        var first = SyntheticRuns.Record(
            SyntheticRuns.Samples(seed: 1),
            runId: "run-1",
            generatedAtUtc: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

        PerformanceHistoryStore.Append(path, first);
        PerformanceHistoryStore.Append(path, first with { Trigger = "workflow_dispatch" });

        var records = PerformanceHistoryStore.Read(path, out _);

        Assert.Equal(1, records.Count);
        Assert.Equal("workflow_dispatch", records[0].Trigger);
    }

    [Test]
    public void DifferentRunsAreBothRecorded()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");

        PerformanceHistoryStore.Append(path, Record(1));
        PerformanceHistoryStore.Append(path, Record(2));

        Assert.Equal(2, PerformanceHistoryStore.Read(path, out _).Count);
    }

    [Test]
    public void RecordsFromAnotherSchemaAreSkippedRatherThanFailing()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");
        File.WriteAllLines(path, ["{\"SchemaVersion\":99,\"Something\":\"else\"}"]);
        PerformanceHistoryStore.Append(path, Record(1));

        var records = PerformanceHistoryStore.Read(path, out var skipped);

        Assert.Equal(1, records.Count);
        Assert.Equal(1, skipped);
    }

    [Test]
    public void AppendingPreservesLinesTheCurrentSchemaCannotRead()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");
        File.WriteAllLines(path, ["{\"SchemaVersion\":99,\"Something\":\"else\"}"]);

        PerformanceHistoryStore.Append(path, Record(1));

        Assert.Equal(2, File.ReadAllLines(path).Length);
    }

    [Test]
    public void BlankLinesAreIgnored()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");
        File.WriteAllLines(path, [PerformanceHistoryStore.Serialize(Record(1)), string.Empty, string.Empty]);

        Assert.Equal(1, PerformanceHistoryStore.Read(path, out _).Count);
    }

    [Test]
    public void AMalformedLineIsReportedWithItsLocation()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Resolve("history.jsonl");
        File.WriteAllLines(path, [PerformanceHistoryStore.Serialize(Record(1)), "{ not json"]);

        var exception = Assert.Throws<InvalidDataException>(() => PerformanceHistoryStore.Read(path, out _));

        Assert.Contains("line 2", exception.Message);
    }

    private static HistoryRecord Record(int index) => SyntheticRuns.Record(
        SyntheticRuns.Samples(seed: index + 1),
        runId: $"run-{index}",
        generatedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(index));

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("nextunit-history-").FullName;

        public string Resolve(string name) => Path.Join(_root, name);

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
