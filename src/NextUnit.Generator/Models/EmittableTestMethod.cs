using System.Collections.Immutable;

namespace NextUnit.Generator.Models;

/// <summary>
/// A test method the expansion cap admitted, carrying the matrix expansion computed for it.
/// </summary>
/// <remarks>
/// Not a pipeline model: it is built inside the source-output callback by
/// <c>TestCaseExpansionValidator</c> and read by <c>RegistryEmitter</c> in the same call, so it
/// never crosses an incremental boundary, never needs value equality, and is never held in Roslyn's
/// cache between compilations. A struct rather than a class because one exists for every admitted
/// test method, and a suite with no <c>[Matrix]</c> at all should not pay an allocation per test
/// for a field it never fills.
/// <para>
/// Carrying the expansion here does keep every matrix method's combinations alive until emission
/// finishes, where computing it twice kept one method's alive at a time. Recomputing to shorten that
/// lifetime was rejected: <c>RegistryEmitter</c> builds the whole registry in one
/// <c>CodeWriter</c> before it returns, and one emitted matrix case is several hundred characters of
/// that text against a combination of a few parameter references, so peak memory already scales as
/// methods x cap and the retained expansions are a fraction of the text they produce.
/// </para>
/// </remarks>
internal readonly struct EmittableTestMethod
{
    private readonly ImmutableArray<EquatableArray<ConstantValue>> _matrixCombinations;

    public EmittableTestMethod(
        TestMethodDescriptor test,
        ImmutableArray<EquatableArray<ConstantValue>> matrixCombinations)
    {
        Test = test;
        _matrixCombinations = matrixCombinations;
    }

    public TestMethodDescriptor Test { get; }

    /// <summary>
    /// Gets the combinations the registry emits for this method, already filtered by
    /// <c>[MatrixExclusion]</c>, and empty for every expansion kind but
    /// <see cref="Helpers.TestExpansionKind.Matrix"/>.
    /// </summary>
    /// <remarks>
    /// Read through <c>IsDefault</c> because a struct can always be default-initialized -- an array
    /// slot, a field, <c>default</c> written by hand -- and a default <see cref="ImmutableArray{T}"/>
    /// throws on <c>Length</c> and on enumeration. Without this, such an instance would fail the
    /// emitter's loop with a <see cref="NullReferenceException"/> out of a struct that reads as
    /// initialized rather than emitting no matrix cases.
    /// </remarks>
    public ImmutableArray<EquatableArray<ConstantValue>> MatrixCombinations =>
        _matrixCombinations.IsDefault
            ? ImmutableArray<EquatableArray<ConstantValue>>.Empty
            : _matrixCombinations;
}
