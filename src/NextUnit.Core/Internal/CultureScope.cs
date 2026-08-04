using System.Globalization;

namespace NextUnit.Internal;

/// <summary>
/// Applies a test's declared cultures for the duration of one attempt and puts back what was there
/// before, whatever ends the attempt.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CultureInfo.CurrentCulture"/> is backed by an <c>AsyncLocal</c>, so a value assigned
/// inside a test's own asynchronous flow belongs to that flow: tests executing concurrently each
/// observe their own culture, and neither can see the other's. That is what makes a declared culture
/// safe without serializing the run, and it is pinned by a parallel test rather than assumed.
/// </para>
/// <para>
/// Restoring in <see cref="Dispose"/> is deliberate belt-and-braces rather than a fix for an
/// observable leak. The engine reaches enough await points around a test that the execution context
/// restored at those boundaries already discards a culture the test assigned: disabling this restore
/// leaves every behavioral test passing. It is kept because that containment is a property of which
/// engine internals happen to suspend rather than a guarantee anyone stated, so an attempt that ran
/// without suspending would silently leak into the following test. Doing it here makes "the culture a
/// test leaves behind dies with the test" structural, for two reference comparisons.
/// </para>
/// </remarks>
internal readonly struct CultureScope : IDisposable
{
    private readonly CultureInfo? _originalCulture;
    private readonly CultureInfo? _originalUICulture;

    private CultureScope(CultureInfo? originalCulture, CultureInfo? originalUICulture)
    {
        _originalCulture = originalCulture;
        _originalUICulture = originalUICulture;
    }

    /// <summary>
    /// Captures the current cultures and applies the declared ones.
    /// </summary>
    /// <param name="culture">The culture to apply, or <c>null</c> to keep the ambient one.</param>
    /// <param name="uiCulture">The UI culture to apply, or <c>null</c> to keep the ambient one.</param>
    /// <remarks>
    /// Both axes are captured even when nothing is applied, because the point is to contain what the
    /// test changes as much as what the engine changes.
    /// </remarks>
    public static CultureScope Enter(CultureInfo? culture, CultureInfo? uiCulture)
    {
        var scope = new CultureScope(CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        if (culture is not null)
        {
            CultureInfo.CurrentCulture = culture;
        }

        if (uiCulture is not null)
        {
            CultureInfo.CurrentUICulture = uiCulture;
        }

        return scope;
    }

    /// <summary>
    /// Resolves the culture names on a descriptor into culture objects.
    /// </summary>
    /// <exception cref="CultureNotFoundException">
    /// Thrown when a declared name matches no culture on this machine. Callers report it against the
    /// test that declared it rather than letting it end the run.
    /// </exception>
    /// <remarks>
    /// <see cref="CultureInfo.GetCultureInfo(string)"/> rather than the constructor: it returns
    /// cached, read-only instances, so repeating this per attempt costs a dictionary lookup, and it
    /// needs no reflection, which keeps the path usable under Native AOT.
    /// </remarks>
    public static (CultureInfo? Culture, CultureInfo? UICulture) Resolve(TestCultureInfo declared)
    {
        var culture = declared.CultureName is null ? null : CultureInfo.GetCultureInfo(declared.CultureName);
        var uiCulture = declared.UICultureName is null ? null : CultureInfo.GetCultureInfo(declared.UICultureName);
        return (culture, uiCulture);
    }

    /// <summary>
    /// Restores the cultures captured when the scope was entered.
    /// </summary>
    public void Dispose()
    {
        // Assigning is not free - it writes through an AsyncLocal and allocates a new execution
        // context - so the common case of a test that never touched the culture writes nothing.
        if (_originalCulture is not null && !ReferenceEquals(CultureInfo.CurrentCulture, _originalCulture))
        {
            CultureInfo.CurrentCulture = _originalCulture;
        }

        if (_originalUICulture is not null && !ReferenceEquals(CultureInfo.CurrentUICulture, _originalUICulture))
        {
            CultureInfo.CurrentUICulture = _originalUICulture;
        }
    }
}
