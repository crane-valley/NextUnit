using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
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
    public static ImmutableArray<ImmutableArray<TypedConstant>> ComputeCartesianProduct(
        ImmutableArray<MatrixParameterDescriptor> matrixParameters)
    {
        if (matrixParameters.IsDefaultOrEmpty)
        {
            return ImmutableArray<ImmutableArray<TypedConstant>>.Empty;
        }

        // Start with an empty combination
        var combinations = ImmutableArray.Create(ImmutableArray<TypedConstant>.Empty);

        foreach (var parameter in matrixParameters)
        {
            var newCombinations = ImmutableArray.CreateBuilder<ImmutableArray<TypedConstant>>();

            foreach (var existingCombination in combinations)
            {
                foreach (var value in parameter.Values)
                {
                    newCombinations.Add(existingCombination.Add(value));
                }
            }

            combinations = newCombinations.ToImmutable();
        }

        return combinations;
    }

    /// <summary>
    /// Filters out excluded combinations from the Cartesian product.
    /// </summary>
    /// <param name="combinations">All combinations from the Cartesian product.</param>
    /// <param name="exclusions">The exclusion patterns to filter out.</param>
    /// <returns>The filtered combinations.</returns>
    public static ImmutableArray<ImmutableArray<TypedConstant>> ApplyExclusions(
        ImmutableArray<ImmutableArray<TypedConstant>> combinations,
        ImmutableArray<MatrixExclusionDescriptor> exclusions)
    {
        if (exclusions.IsDefaultOrEmpty)
        {
            return combinations;
        }

        var builder = ImmutableArray.CreateBuilder<ImmutableArray<TypedConstant>>();

        foreach (var combination in combinations)
        {
            if (!IsExcluded(combination, exclusions))
            {
                builder.Add(combination);
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsExcluded(
        ImmutableArray<TypedConstant> combination,
        ImmutableArray<MatrixExclusionDescriptor> exclusions)
    {
        foreach (var exclusion in exclusions)
        {
            if (MatchesExclusion(combination, exclusion.Values))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesExclusion(
        ImmutableArray<TypedConstant> combination,
        ImmutableArray<TypedConstant> exclusionValues)
    {
        // Exclusion must have the same number of values as the combination
        if (combination.Length != exclusionValues.Length)
        {
            return false;
        }

        for (var i = 0; i < combination.Length; i++)
        {
            if (!TypedConstantsEqual(combination[i], exclusionValues[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TypedConstantsEqual(TypedConstant a, TypedConstant b)
    {
        // Handle null values
        if (a.IsNull && b.IsNull)
        {
            return true;
        }

        if (a.IsNull || b.IsNull)
        {
            return false;
        }

        // Compare values directly
        return Equals(a.Value, b.Value);
    }
}
