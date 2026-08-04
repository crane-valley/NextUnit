namespace NextUnit;

/// <summary>
/// Runs the annotated tests with <see cref="System.Globalization.CultureInfo.CurrentCulture"/> set to
/// the named culture, and restores the previous culture afterwards.
/// </summary>
/// <remarks>
/// <para>
/// The culture applies to the whole test attempt: the test class constructor, test-scoped
/// <c>[Before]</c> and <c>[After]</c> hooks, the test method, and disposal. It is applied again for
/// every <see cref="RetryAttribute"/> attempt, so an attempt that changes the culture cannot decide
/// what the next attempt starts from.
/// </para>
/// <para>
/// Resolution is per axis and most specific wins: a method-level attribute overrides a class-level
/// one, which overrides an assembly-level one. <see cref="UICultureAttribute"/> resolves separately,
/// so a method may override the current culture while inheriting the UI culture from its class or
/// assembly.
/// </para>
/// <para>
/// The culture is set inside the test's own asynchronous flow, so it neither reaches nor is reached
/// by tests running concurrently. Restoration is unconditional: it happens after a pass, a failure,
/// a timeout, and a cancellation alike.
/// </para>
/// <para>
/// Display names are built during discovery rather than execution, so a declared culture does not
/// change them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [Culture("de-DE")]
/// public void ParsesGermanDecimalSeparator()
/// {
///     Assert.Equal(1234.5, double.Parse("1234,5"));
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CultureAttribute : Attribute
{
    /// <summary>
    /// Gets the culture name, for example <c>"ja-JP"</c>. The empty string is the invariant culture.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CultureAttribute"/> class.
    /// </summary>
    /// <param name="name">
    /// The culture name passed to <see cref="System.Globalization.CultureInfo.GetCultureInfo(string)"/>.
    /// Use the empty string for the invariant culture, or <see cref="InvariantCultureAttribute"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <remarks>
    /// A name that no culture matches is reported as a test error when the test runs, not when the
    /// attribute is constructed: whether a culture exists depends on the machine executing the test,
    /// and failing at attribute construction would take down discovery instead of one test.
    /// </remarks>
    public CultureAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

/// <summary>
/// Runs the annotated tests with <see cref="System.Globalization.CultureInfo.CurrentUICulture"/> set
/// to the named culture, and restores the previous UI culture afterwards.
/// </summary>
/// <remarks>
/// Scoping, precedence, retry, and restoration behave exactly as described on
/// <see cref="CultureAttribute"/>; only the axis differs. The UI culture selects localized resources,
/// which is what framework and library messages are looked up with.
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [UICulture("ja-JP")]
/// public void LooksUpJapaneseResources()
/// {
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class UICultureAttribute : Attribute
{
    /// <summary>
    /// Gets the UI culture name, for example <c>"ja-JP"</c>. The empty string is the invariant culture.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UICultureAttribute"/> class.
    /// </summary>
    /// <param name="name">
    /// The culture name passed to <see cref="System.Globalization.CultureInfo.GetCultureInfo(string)"/>.
    /// Use the empty string for the invariant culture, or <see cref="InvariantCultureAttribute"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public UICultureAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

/// <summary>
/// Runs the annotated tests with both the current culture and the UI culture set to the invariant
/// culture, and restores the previous cultures afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Shorthand for <c>[Culture("")]</c> combined with <c>[UICulture("")]</c>, which is the common way
/// to make a test independent of the machine it runs on.
/// </para>
/// <para>
/// It supplies only the axes left unspecified at its own level, so an explicit
/// <see cref="CultureAttribute"/> or <see cref="UICultureAttribute"/> alongside it still wins for its
/// own axis. <c>[InvariantCulture]</c> with <c>[UICulture("ja-JP")]</c> therefore means invariant
/// formatting with Japanese resources rather than a conflict.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [InvariantCulture]
/// public class FormattingTests
/// {
///     [Test]
///     public void FormatsIndependentlyOfTheMachineCulture()
///     {
///         Assert.Equal("1234.5", 1234.5.ToString());
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InvariantCultureAttribute : Attribute
{
}
