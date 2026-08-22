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
/// Runs over what the registry actually emits, not over every declaration in the compilation. A
/// hook on a class that holds no tests and that nothing derives from is never emitted, and neither
/// is one whose scope the emitter does not place per descriptor, or one a nearer declaration
/// supersedes: none of those has ever failed a build and none starts now. The selection comes from
/// <see cref="LifecycleSelection"/> so this cannot drift from what the emitter chose.
/// <para>
/// <c>NEXTUNIT017</c> is the deliberate exception, and it is one because the shape it reports is
/// uncallable in every compilation rather than in this one: see
/// <see cref="ReportExplicitInterfaceMethod"/>.
/// </para>
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
        // it is reported once rather than once per test case. Keyed on the override chain, which is
        // how the rest of the generator identifies one hook: a method name collapses overloads that
        // C# keeps apart, and silencing the report of a second `Setup(CancellationToken)` is exactly
        // the silence these rules exist to remove.
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in beforeLifecycle)
        {
            ReportDeclaredMethod(context, method, reported);
        }

        foreach (var method in afterLifecycle)
        {
            ReportDeclaredMethod(context, method, reported);
        }

        foreach (var test in tests)
        {
            var declared = declaredByType.TryGetValue(test.FullyQualifiedTypeName, out var methods)
                ? methods
                : new List<LifecycleMethodDescriptor>();
            var lifecycle = TestLifecycleMethods.Create(test.InheritedLifecycleMethods, declared);

            foreach (var scope in LifecycleSelection.PerDescriptorScopes)
            {
                ReportUnreachableSelection(context, lifecycle, isBefore: true, scope, reported);
                ReportUnreachableSelection(context, lifecycle, isBefore: false, scope, reported);
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
    /// Mirrors the selection <c>GlobalLifecycleMethods.Collect</c> makes, including its instance
    /// method exclusion, so a hook that is never emitted is never reported.
    /// </remarks>
    private static bool IsGlobalScope(LifecycleMethodDescriptor method) =>
        method.IsStatic &&
        (method.BeforeScopes.Contains(LifecycleScopeConstants.Assembly) ||
         method.AfterScopes.Contains(LifecycleScopeConstants.Assembly) ||
         method.BeforeScopes.Contains(LifecycleScopeConstants.Session) ||
         method.AfterScopes.Contains(LifecycleScopeConstants.Session));

    /// <summary>
    /// Reports a hook declared as an explicit interface implementation, at its declaration.
    /// </summary>
    /// <remarks>
    /// The one rule here that does not wait for a use site, because the declaring assembly is the
    /// only compilation that can see the declaration at all: metadata is imported with
    /// <c>MetadataImportOptions.Public</c>, so a consumer deriving from such a base class never
    /// receives the member and cannot report anything. Waiting for a test would therefore let the
    /// shape ship silently in a shared fixture package, which is the failure this closes.
    /// <para>
    /// Gated on the scopes the registry emits, exactly as the rest of this validator is. A hook
    /// declaring only <c>Assembly</c> or <c>Session</c> on an instance method is dropped for being
    /// an instance method, so reporting the declaration form there would name a remedy that leaves
    /// the hook just as dead.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Reports what one declaration is worth reporting on its own, before any test is considered.
    /// </summary>
    /// <remarks>
    /// The declaration-form rule goes first, so the shared set leaves the accessibility rule silent
    /// about a declaration this one has already reported with the remedy that actually applies to
    /// it. Ordering the two questions inside one pass rather than running two passes is safe because
    /// they cannot interact across declarations: the set is keyed on the override chain, so two
    /// entries share a key only when they are the same method, and a method is an explicit interface
    /// implementation or is not.
    /// </remarks>
    private static void ReportDeclaredMethod(
        SourceProductionContext context,
        LifecycleMethodDescriptor method,
        HashSet<string> reported)
    {
        ReportExplicitInterfaceMethod(context, method, reported);

        if (IsGlobalScope(method))
        {
            ReportUnreachableMethod(context, method, reported);
        }
    }

    private static void ReportExplicitInterfaceMethod(
        SourceProductionContext context,
        LifecycleMethodDescriptor method,
        HashSet<string> reported)
    {
        if (!method.IsExplicitInterfaceImplementation ||
            !IsEmittedScope(method) ||
            !reported.Add(method.OverrideRootId))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            GeneratorDiagnosticDescriptors.LifecycleMethodIsExplicitInterfaceImplementation,
            Location.None,
            method.FullyQualifiedTypeName,
            method.MethodName));
    }

    /// <summary>
    /// Whether the registry emits the hook for someone -- per test descriptor, or once into the
    /// registry's static properties.
    /// </summary>
    private static bool IsEmittedScope(LifecycleMethodDescriptor method)
    {
        foreach (var scope in LifecycleSelection.PerDescriptorScopes)
        {
            if (method.BeforeScopes.Contains(scope) || method.AfterScopes.Contains(scope))
            {
                return true;
            }
        }

        return IsGlobalScope(method);
    }

    private static void ReportUnreachableSelection(
        SourceProductionContext context,
        TestLifecycleMethods lifecycle,
        bool isBefore,
        int scope,
        HashSet<string> reported)
    {
        foreach (var method in LifecycleSelection.Select(lifecycle, isBefore, scope))
        {
            ReportUnreachableMethod(context, method, reported);
        }
    }

    private static void ReportUnreachableMethod(
        SourceProductionContext context,
        LifecycleMethodDescriptor method,
        HashSet<string> reported)
    {
        if (method.IsReachable || !reported.Add(method.OverrideRootId))
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
