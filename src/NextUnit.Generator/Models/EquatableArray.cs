using System.Collections;
using System.Collections.Immutable;

namespace NextUnit.Generator.Models;

/// <summary>
/// An <see cref="ImmutableArray{T}"/> wrapper that compares by value.
/// </summary>
/// <remarks>
/// Incremental generator pipeline models must have value equality: <see cref="ImmutableArray{T}"/>
/// compares by the underlying array reference, so a model holding one never equals a freshly built
/// copy and every rerun invalidates the cached source output.
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _values;

    public EquatableArray(ImmutableArray<T> values)
    {
        _values = values;
    }

    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public int Length => _values.IsDefault ? 0 : _values.Length;

    public bool IsDefaultOrEmpty => _values.IsDefaultOrEmpty;

    public T this[int index] => _values[index];

    public static implicit operator EquatableArray<T>(ImmutableArray<T> values) => new(values);

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public bool Equals(EquatableArray<T> other)
    {
        if (_values.IsDefault || other._values.IsDefault)
        {
            return _values.IsDefault && other._values.IsDefault;
        }

        if (_values.Length != other._values.Length)
        {
            return false;
        }

        // Indexed loop instead of SequenceEqual: this comparison runs for every cached pipeline
        // model on every incremental pass, and the enumerable-based path allocates enumerator boxes.
        // The default comparer stays null-safe for reference-type elements without boxing.
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < _values.Length; i++)
        {
            if (!comparer.Equals(_values[i], other._values[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_values.IsDefaultOrEmpty)
        {
            return 0;
        }

        unchecked
        {
            var hash = 17;
            foreach (var value in _values)
            {
                hash = (hash * 31) + (value?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    // The struct enumerator keeps foreach allocation-free; the interface implementations below
    // exist only for LINQ consumers, which pay for the box regardless.
    public ImmutableArray<T>.Enumerator GetEnumerator() =>
        (_values.IsDefault ? ImmutableArray<T>.Empty : _values).GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() =>
        ((IEnumerable<T>)(_values.IsDefault ? ImmutableArray<T>.Empty : _values)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();
}
