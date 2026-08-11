using System.Globalization;
using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Pins that display names built at run time - the ones a <c>[TestData]</c>,
/// <c>[ClassDataSource]</c>, or combined-source row gets - are formatted with the invariant culture,
/// so a reported test name is a property of the test rather than of the machine running it.
/// </summary>
/// <remarks>
/// Each test asserts a literal name and first proves the ambient culture would have produced a
/// different one; deriving the expectation from the same formatting the product uses would make the
/// assertions pass no matter which culture the product picked.
/// </remarks>
public sealed class DisplayNameBuilderCultureTests
{
    private static DateTime SampleDate { get; } = new(2026, 8, 4, 13, 5, 0, DateTimeKind.Unspecified);

    [Test]
    public void Build_DoubleRow_UsesTheInvariantDecimalSeparator()
    {
        using var ambient = AmbientCulture.Set("de-DE");
        Assert.NotEqual("1234.5", 1234.5.ToString(CultureInfo.CurrentCulture));

        var name = Build(1234.5);

        Assert.Equal("Run(1234.5)", name);
    }

    [Test]
    public void Build_DecimalRow_UsesTheInvariantDecimalSeparator()
    {
        using var ambient = AmbientCulture.Set("de-DE");
        Assert.NotEqual("1234.5", 1234.5m.ToString(CultureInfo.CurrentCulture));

        var name = Build(1234.5m);

        Assert.Equal("Run(1234.5)", name);
    }

    [Test]
    public void Build_DateTimeRow_UsesTheInvariantDateFormat()
    {
        using var ambient = AmbientCulture.Set("de-DE");
        Assert.NotEqual("08/04/2026 13:05:00", SampleDate.ToString(CultureInfo.CurrentCulture));

        var name = Build(SampleDate);

        Assert.Equal("Run(08/04/2026 13:05:00)", name);
    }

    [Test]
    public void Build_NegativeIntegerRow_UsesTheInvariantNegativeSign()
    {
        using var ambient = AmbientCulture.Set("sv-SE");
        Assert.NotEqual("-5", (-5).ToString(CultureInfo.CurrentCulture));

        var name = Build(-5);

        Assert.Equal("Run(-5)", name);
    }

    [Test]
    public void FormatWithPlaceholders_DoubleArgument_UsesTheInvariantDecimalSeparator()
    {
        using var ambient = AmbientCulture.Set("de-DE");
        Assert.NotEqual("1234.5", 1234.5.ToString(CultureInfo.CurrentCulture));

        var name = DisplayNameBuilder.FormatWithPlaceholders("ratio is {0}", [1234.5]);

        Assert.Equal("ratio is 1234.5", name);
    }

    private static string Build(object? argument) =>
        DisplayNameBuilder.Build(
            "Run",
            customDisplayNameTemplate: null,
            formatterType: null,
            testClass: typeof(DisplayNameBuilderCultureTests),
            arguments: [argument],
            argumentSetIndex: 0);

    /// <summary>
    /// Pins the ambient culture for the duration of a test and puts back what was there.
    /// </summary>
    private readonly struct AmbientCulture : IDisposable
    {
        private readonly CultureInfo _culture;

        private AmbientCulture(CultureInfo culture)
        {
            _culture = culture;
        }

        public static AmbientCulture Set(string name)
        {
            var scope = new AmbientCulture(CultureInfo.CurrentCulture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            return scope;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
        }
    }
}
