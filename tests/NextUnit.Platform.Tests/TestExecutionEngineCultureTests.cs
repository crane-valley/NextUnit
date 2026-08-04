using System.Globalization;
using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Behavioral coverage for declared cultures: what a test observes, what survives the test, and what
/// a concurrently running test can see.
/// </summary>
/// <remarks>
/// Every assertion is on a value the test itself observed and captured into a local, rather than on
/// the ambient culture after the run. The engine restores what it captured, so reading the ambient
/// culture afterwards proves only that restoration happened - not that the test ever ran under the
/// declared culture.
/// </remarks>
public sealed class TestExecutionEngineCultureTests
{
    // Chosen because the differences are ASCII: this date formats as 2026/08/04 under ja-JP,
    // 8/4/2026 under en-US, and 08/04/2026 under the invariant culture, so the assertions do not
    // depend on localized month or day names.
    private static DateTime SampleDate { get; } = new(2026, 8, 4);

    [Test]
    public async Task DeclaredCulture_AppliesToTheTestBodyAsync()
    {
        string? observed = null;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.applies")
            .WithCulture(cultureName: "ja-JP")
            .WithMethod((_, _) =>
            {
                observed = SampleDate.ToString("d", CultureInfo.CurrentCulture);
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Empty(sink.Errors);
        Assert.Equal("2026/08/04", observed);
    }

    [Test]
    public async Task DeclaredCulture_ChangesFormattingParsingAndAssertionMessagesAsync()
    {
        string? formatted = null;
        double parsed = 0;
        string? assertionMessage = null;

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.de")
            .WithCulture(cultureName: "de-DE")
            .WithMethod((_, _) =>
            {
                // Formatting and parsing both read CultureInfo.CurrentCulture through the
                // no-provider overloads, which is exactly what a declared culture is for.
                formatted = 1234.5.ToString();
                parsed = double.Parse("1234,5");

                try
                {
                    Assert.Equal(1234.5, 9876.5);
                }
                catch (AssertionFailedException ex)
                {
                    assertionMessage = ex.Message;
                }

                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Empty(sink.Errors);
        Assert.Equal("1234,5", formatted);
        Assert.Equal(1234.5, parsed);

        // The assertion message interpolates the values, so it follows the declared culture too.
        Assert.Contains("1234,5", assertionMessage);
    }

    [Test]
    public async Task InvariantCulture_FormatsIndependentlyOfTheMachineAsync()
    {
        string? number = null;
        string? date = null;

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.invariant")
            .WithCulture(cultureName: "", uiCultureName: "")
            .WithMethod((_, _) =>
            {
                number = 1234.5.ToString();
                date = SampleDate.ToString("d", CultureInfo.CurrentCulture);
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Empty(sink.Errors);
        Assert.Equal("1234.5", number);
        Assert.Equal("08/04/2026", date);
    }

    [Test]
    public async Task EnUsAndJaJp_ObserveTheirOwnFormattingAsync()
    {
        string? enUs = null;
        string? jaJp = null;

        var enUsTest = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.en-US")
            .WithCulture(cultureName: "en-US")
            .WithMethod((_, _) =>
            {
                enUs = SampleDate.ToString("d", CultureInfo.CurrentCulture);
                return Task.CompletedTask;
            })
            .Build();

        var jaJpTest = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.ja-JP")
            .WithCulture(cultureName: "ja-JP")
            .WithMethod((_, _) =>
            {
                jaJp = SampleDate.ToString("d", CultureInfo.CurrentCulture);
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([enUsTest, jaJpTest], sink, CancellationToken.None);

        Assert.Empty(sink.Errors);
        Assert.Equal("8/4/2026", enUs);
        Assert.Equal("2026/08/04", jaJp);
    }

    [Test]
    public async Task DeclaredUICulture_AppliesSeparatelyFromTheCurrentCultureAsync()
    {
        string? culture = null;
        string? uiCulture = null;

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.ui")
            .WithCulture(cultureName: "en-US", uiCultureName: "ja-JP")
            .WithMethod((_, _) =>
            {
                culture = CultureInfo.CurrentCulture.Name;
                uiCulture = CultureInfo.CurrentUICulture.Name;
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Empty(sink.Errors);
        Assert.Equal("en-US", culture);
        Assert.Equal("ja-JP", uiCulture);
    }

    [Test]
    public async Task UndeclaredAxis_LeavesTheAmbientCultureAloneAsync()
    {
        using var ambient = AmbientCulture.Set("en-US");
        string? uiCulture = null;

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.partial")
            .WithCulture(cultureName: "ja-JP")
            .WithMethod((_, _) =>
            {
                uiCulture = CultureInfo.CurrentUICulture.Name;
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Empty(sink.Errors);
        Assert.Equal("en-US", uiCulture);
    }

    [Test]
    public async Task PassingTest_RestoresTheCultureAsync()
    {
        await AssertCultureRestoredAsync(
            "culture.restore.pass",
            builder => builder.WithMethod((_, _) => Task.CompletedTask));
    }

    [Test]
    public async Task FailingTest_RestoresTheCultureAsync()
    {
        await AssertCultureRestoredAsync(
            "culture.restore.fail",
            builder => builder.WithMethod((_, _) => throw new InvalidOperationException("boom")));
    }

    [Test]
    public async Task TimedOutTest_RestoresTheCultureAsync()
    {
        await AssertCultureRestoredAsync(
            "culture.restore.timeout",
            builder => builder
                .WithTimeout(50)
                .WithMethod(async (_, ct) => await Task.Delay(TimeSpan.FromSeconds(30), ct)));
    }

    [Test]
    public async Task CancelledRun_RestoresTheCultureAsync()
    {
        using var ambient = AmbientCulture.Set("en-US");
        using var cts = new CancellationTokenSource();

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.restore.cancel")
            .WithCulture(cultureName: "de-DE")
            .WithMethod(async (_, ct) =>
            {
                await cts.CancelAsync();
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            })
            .Build();

        var sink = new RecordingSink();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new TestExecutionEngine().RunAsync([test], sink, cts.Token));

        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
    }

    /// <summary>
    /// A test that changes the culture without declaring one must not decide what the next test sees.
    /// </summary>
    /// <remarks>
    /// The guarantee, not the mechanism. Two things enforce it today: the execution context the
    /// engine restores at its own await points, and the explicit restore in <c>CultureScope</c>.
    /// Disabling the latter leaves this test passing, so it is a regression guard on the observable
    /// behavior rather than proof of which half provides it.
    /// </remarks>
    [Test]
    public async Task TestThatChangesCulture_DoesNotContaminateTheNextTestAsync()
    {
        using var ambient = AmbientCulture.Set("en-US");
        string? observedByNextTest = null;

        var mutating = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.leak.mutator")
            .Serial()
            .WithMethod((_, _) =>
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                return Task.CompletedTask;
            })
            .Build();

        var following = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.leak.observer")
            .Serial()
            .WithMethod((_, _) =>
            {
                observedByNextTest = CultureInfo.CurrentCulture.Name;
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([mutating, following], sink, CancellationToken.None);

        Assert.Empty(sink.Errors);
        Assert.Equal("en-US", observedByNextTest);
    }

    /// <summary>
    /// Proves the concurrency claim rather than asserting it: two tests that overlap in time each
    /// observe only their own declared culture, across many suspension points.
    /// </summary>
    [Test]
    public async Task ConcurrentTests_DoNotSeeEachOthersCultureAsync()
    {
        const int Yields = 60;
        var jaObservations = new List<string>();
        var deObservations = new List<string>();

        // Rendezvous by awaiting rather than blocking: a blocking wait would deadlock if the engine
        // ever ran the two serially. The timeout keeps a serial run from hanging the suite, and
        // OverlapConfirmed records whether the two were genuinely in flight together.
        var jaStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var jaSawDe = false;
        var deSawJa = false;

        var jaTest = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.parallel.ja")
            .WithMethod(async (_, _) =>
            {
                jaStarted.TrySetResult();
                jaSawDe = await ReachedRendezvousAsync(deStarted.Task);

                for (var i = 0; i < Yields; i++)
                {
                    await Task.Yield();
                    lock (jaObservations)
                    {
                        jaObservations.Add(CultureInfo.CurrentCulture.Name);
                    }
                }
            })
            .WithCulture(cultureName: "ja-JP")
            .Build();

        var deTest = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.parallel.de")
            .WithMethod(async (_, _) =>
            {
                deStarted.TrySetResult();
                deSawJa = await ReachedRendezvousAsync(jaStarted.Task);

                for (var i = 0; i < Yields; i++)
                {
                    await Task.Yield();
                    lock (deObservations)
                    {
                        deObservations.Add(CultureInfo.CurrentCulture.Name);
                    }
                }
            })
            .WithCulture(cultureName: "de-DE")
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([jaTest, deTest], sink, CancellationToken.None);

        Assert.Empty(sink.Errors);

        // Without this the test could pass on a machine that ran the two sequentially, proving
        // nothing about concurrency.
        Assert.True(jaSawDe && deSawJa, "The two tests did not overlap, so isolation was not exercised.");

        Assert.Equal(Yields, jaObservations.Count);
        Assert.Equal(Yields, deObservations.Count);
        Assert.All(jaObservations, name => Assert.Equal("ja-JP", name));
        Assert.All(deObservations, name => Assert.Equal("de-DE", name));
    }

    /// <summary>
    /// Each retry attempt starts from the declared culture, so an attempt that changes it cannot
    /// decide what the next attempt runs under.
    /// </summary>
    [Test]
    public async Task EachRetryAttempt_StartsFromTheDeclaredCultureAsync()
    {
        var observedAtAttemptStart = new List<string>();

        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.retry")
            .WithCulture(cultureName: "ja-JP")
            .WithRetry(3, delayMs: 0)
            .WithMethod((_, _) =>
            {
                observedAtAttemptStart.Add(CultureInfo.CurrentCulture.Name);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                throw new InvalidOperationException("boom");
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.Equal(3, observedAtAttemptStart.Count);
        Assert.All(observedAtAttemptStart, name => Assert.Equal("ja-JP", name));
    }

    [Test]
    public async Task UnavailableCulture_IsReportedAgainstTheTestAsync()
    {
        var ran = false;
        var test = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.missing")
            .WithCulture(cultureName: "not a culture")
            .WithMethod((_, _) =>
            {
                ran = true;
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([test], sink, CancellationToken.None);

        Assert.False(ran);
        var (descriptor, exception) = Assert.Single(sink.Errors);
        Assert.Equal("culture.missing", descriptor.Id.Value);
        Assert.Contains("not a culture", exception.Message);
        Assert.Equal(typeof(CultureNotFoundException), exception.InnerException?.GetType());
    }

    /// <summary>
    /// One unavailable culture fails its own test and leaves the rest of the run alone.
    /// </summary>
    [Test]
    public async Task UnavailableCulture_DoesNotEndTheRunAsync()
    {
        var healthyRan = false;

        var broken = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.missing.first")
            .Serial()
            .WithCulture(cultureName: "not a culture")
            .WithMethod((_, _) => Task.CompletedTask)
            .Build();

        var healthy = TestCaseDescriptorBuilder
            .For<SampleTestClass>("culture.missing.second")
            .Serial()
            .WithMethod((_, _) =>
            {
                healthyRan = true;
                return Task.CompletedTask;
            })
            .Build();

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([broken, healthy], sink, CancellationToken.None);

        Assert.True(healthyRan);
        Assert.Single(sink.Errors);
        Assert.Single(sink.Passed);
    }

    private static async Task<bool> ReachedRendezvousAsync(Task other)
    {
        var completed = await Task.WhenAny(other, Task.Delay(TimeSpan.FromSeconds(10)));
        return ReferenceEquals(completed, other);
    }

    private static async Task AssertCultureRestoredAsync(
        string id,
        Func<TestCaseDescriptorBuilder, TestCaseDescriptorBuilder> configure)
    {
        // A known baseline rather than whatever the machine defaults to: on a ja-JP machine a test
        // declaring ja-JP would compare the culture against itself and assert nothing.
        using var ambient = AmbientCulture.Set("en-US");

        var builder = TestCaseDescriptorBuilder
            .For<SampleTestClass>(id)
            .WithCulture(cultureName: "de-DE", uiCultureName: "fr-FR");

        var sink = new RecordingSink();
        await new TestExecutionEngine().RunAsync([configure(builder).Build()], sink, CancellationToken.None);

        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
    }

    /// <summary>
    /// Pins the ambient cultures for the duration of an assertion and puts back what was there.
    /// </summary>
    private readonly struct AmbientCulture : IDisposable
    {
        private readonly CultureInfo _culture;
        private readonly CultureInfo _uiCulture;

        private AmbientCulture(CultureInfo culture, CultureInfo uiCulture)
        {
            _culture = culture;
            _uiCulture = uiCulture;
        }

        public static AmbientCulture Set(string name)
        {
            var scope = new AmbientCulture(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            return scope;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
