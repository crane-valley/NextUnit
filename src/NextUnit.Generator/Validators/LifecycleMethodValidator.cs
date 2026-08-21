using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using NextUnit.Generator.Diagnostics;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Validators;

/// <summary>
/// Reports the hooks and attribute types the generated registry would have to name and cannot.
/// </summary>
/// <remarks>
/// Runs over what the registry actually emits, not over every declaration in the compilation: a
/// hook on a class that holds no tests and that nothing derives from is never emitted, so it has
/// never failed a build and does not start now.
/// </remarks>
internal static class LifecycleMethodValidator
{
    public static void ValidateAll(
        SourceProductionContext context,
        ImmutableArray<TestMethodDescriptor> tests,
        ImmutableArray<LifecycleMethodDescriptor> beforeLifecycle,
        ImmutableArray<LifecycleMethodDescriptor> afterLifecycle)
    {
        var declaredByType = beforeLifecycle
            .Concat(afterLifecycle)
            .GroupBy(method => method.FullyQualifiedTypeName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        // One declaration reaches every test of its class and every test of every derived class, so
        // it is reported once rather than once per test case.
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in beforeLifecycle.Concat(afterLifecycle))
        {
            if (IsGlobalScope(method))
            {
                ReportUnreachableMethod(context, method, reported);
            }
        }

        foreach (var test in tests)
        {
            if (declaredByType.TryGetValue(test.FullyQualifiedTypeName, out var declared))
            {
                foreach (var method in declared)
                {
                    ReportUnreachableMethod(context, method, reported);
                }
            }

            for (var i = 0; i < test.InheritedLifecycleMethods.Length; i++)
            {
                ReportUnreachableMethod(context, test.InheritedLifecycleMethods[i], reported);
            }

            if (test.UnreachableInheritedTypeName is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.InheritedTypeNotAccessible,
                    Location.None,
                    test.Id,
                    test.UnreachableInheritedTypeName));
            }
        }
    }

    /// <summary>
    /// Whether the hook is emitted into the registry static properties rather than per descriptor.
    /// </summary>
    /// <remarks>
    /// Mirrors the selection the emitter makes, so a hook that is never emitted is never reported.
    /// </remarks>
    private static bool IsGlobalScope(LifecycleMethodDescriptor method) =>
        method.IsStatic &&
        (method.BeforeScopes.Contains(LifecycleScopeConstants.Assembly) ||
         method.AfterScopes.Contains(LifecycleScopeConstants.Assembly) ||
         method.BeforeScopes.Contains(LifecycleScopeConstants.Session) ||
         method.AfterScopes.Contains(LifecycleScopeConstants.Session));

    private static void ReportUnreachableMethod(
        SourceProductionContext context,
        LifecycleMethodDescriptor method,
        HashSet<string> reported)
    {
        if (method.IsReachable ||
            !reported.Add($"{method.FullyQualifiedTypeName}.{method.MethodName}"))
        {
            return;
        }

        // The pipeline models carry no syntax reference, so every generator diagnostic reports at
        // Location.None; the message names the declaring type and the method instead.
        context.ReportDiagnostic(Diagnostic.Create(
            GeneratorDiagnosticDescriptors.LifecycleMethodNotAccessible,
            Location.None,
            method.FullyQualifiedTypeName,
            method.MethodName));
    }
}
