using System.Collections.Immutable;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Helpers;

/// <summary>
/// Helper methods for computing matrix test combinations.
/// </summary>
internal static class MatrixHelper
{
    /// <summary>
    /// Computes the Cartesian product of all matrix parameter values.
    /// </summary>
    /// <param name="matrixParameters">The matrix parameters with their values.</param>
    /// <returns>All combinations as an array of value arrays.</returns>
    public static ImmutableArray<EquatableArray<ConstantValue>> ComputeCartesianProduct(
        EquatableArray<MatrixParameterDescriptor> matrixParameters)
    {
        if (matrixParameters.IsDefaultOrEmpty)
        {
            return ImmutableArray<EquatableArray<ConstantValue>>.Empty;
        }

        // Start with an empty combination
        var combinations = new List<ImmutableArray<ConstantValue>> { ImmutableArray<ConstantValue>.Empty };

        foreach (var parameter in matrixParameters)
        {
            var newCombinations = new List<ImmutableArray<ConstantValue>>();

            foreach (var existingCombination in combinations)
            {
                foreach (var value in parameter.Values)
                {
                    newCombinations.Add(existingCombination.Add(value));
                }
            }

            combinations = newCombinations;
        }

        return combinations
            .Select(static combination => new EquatableArray<ConstantValue>(combination))
            .ToImmutableArray();
    }

    /// <summary>
    /// Filters out excluded combinations from the Cartesian product.
    /// </summary>
    /// <param name="combinations">All combinations from the Cartesian product.</param>
    /// <param name="exclusions">The exclusion patterns to filter out.</param>
    /// <returns>The filtered combinations.</returns>
    public static ImmutableArray<EquatableArray<ConstantValue>> ApplyExclusions(
        ImmutableArray<EquatableArray<ConstantValue>> combinations,
        EquatableArray<MatrixExclusionDescriptor> exclusions)
    {
        if (exclusions.IsDefaultOrEmpty)
        {
            return combinations;
        }

        return combinations
            .Where(combination => !IsExcluded(combination, exclusions))
            .ToImmutableArray();
    }

    private static bool IsExcluded(
        EquatableArray<ConstantValue> combination,
        EquatableArray<MatrixExclusionDescriptor> exclusions)
    {
        return exclusions.Any(exclusion => MatchesExclusion(combination, exclusion.Values));
    }

    private static bool MatchesExclusion(
        EquatableArray<ConstantValue> combination,
        EquatableArray<ConstantValue> exclusionValues)
    {
        // Exclusion must have the same number of values as the combination
        if (combination.Length != exclusionValues.Length)
        {
            return false;
        }

        for (var i = 0; i < combination.Length; i++)
        {
            // EqualityKey reproduces the value-based comparison this matcher ran on TypedConstant.Value.
            if (!string.Equals(combination[i].EqualityKey, exclusionValues[i].EqualityKey, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
