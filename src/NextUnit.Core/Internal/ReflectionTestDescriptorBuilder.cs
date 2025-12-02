using System.Reflection;

namespace NextUnit.Internal;

/// <summary>
/// Builds <see cref="TestCaseDescriptor"/> instances using reflection.
/// </summary>
/// <remarks>
/// ?? WARNING: This is a DEVELOPMENT-ONLY fallback and will be removed before v1.0.
/// Production code must use the source generator exclusively.
/// This class exists only to enable prototyping and should be feature-flagged or removed.
/// </remarks>
internal static class ReflectionTestDescriptorBuilder
{
    public static IReadOnlyList<TestCaseDescriptor> Build(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var descriptors = new List<TestCaseDescriptor>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract)
            {
                continue;
            }

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var lifecycle = BuildLifecycleInfo(methods);

            foreach (var method in methods)
            {
                if (!method.GetCustomAttributes(typeof(TestAttribute), inherit: false).Any())
                {
                    continue;
                }

                var testDelegate = CreateTestMethodDelegate(type, method);

                var descriptor = new TestCaseDescriptor
                {
                    Id = new TestCaseId(CreateTestId(type, method)),
                    DisplayName = method.Name,
                    TestClass = type,
                    MethodName = method.Name,
                    TestMethod = testDelegate,
                    Lifecycle = lifecycle,
                    Parallel = BuildParallelInfo(type, method),
                    Dependencies = BuildDependencies(method),
                    IsSkipped = false,
                    SkipReason = null
                };

                descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    private static TestMethodDelegate CreateTestMethodDelegate(Type type, MethodInfo method)
    {
        return async (instance, ct) =>
        {
            var target = method.IsStatic ? null : instance;
            var parameters = method.GetParameters();

            object? result;

            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(CancellationToken))
            {
                result = method.Invoke(target, new object[] { ct });
            }
            else if (parameters.Length == 0)
            {
                result = method.Invoke(target, null);
            }
            else
            {
                throw new NotSupportedException(
                    $"Test method '{type.FullName}.{method.Name}' has unsupported parameters.");
            }

            if (result is Task task)
            {
                await task.ConfigureAwait(false);
            }
            else if (result is ValueTask valueTask)
            {
                await valueTask.AsTask().ConfigureAwait(false);
            }
        };
    }

    private static LifecycleMethodDelegate CreateLifecycleMethodDelegate(MethodInfo method)
    {
        return async (instance, ct) =>
        {
            var target = method.IsStatic ? null : instance;
            var parameters = method.GetParameters();

            object? result;

            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(CancellationToken))
            {
                result = method.Invoke(target, new object[] { ct });
            }
            else
            {
                result = method.Invoke(target, null);
            }

            if (result is Task task)
            {
                await task.ConfigureAwait(false);
            }
            else if (result is ValueTask valueTask)
            {
                await valueTask.AsTask().ConfigureAwait(false);
            }
        };
    }

    private static LifecycleInfo BuildLifecycleInfo(IEnumerable<MethodInfo> methods)
    {
        var before = new List<LifecycleMethodDelegate>();
        var after = new List<LifecycleMethodDelegate>();

        foreach (var method in methods)
        {
            foreach (BeforeAttribute attribute in method.GetCustomAttributes(typeof(BeforeAttribute), inherit: false))
            {
                if (attribute.Scope == LifecycleScope.Test)
                {
                    before.Add(CreateLifecycleMethodDelegate(method));
                }
            }

            foreach (AfterAttribute attribute in method.GetCustomAttributes(typeof(AfterAttribute), inherit: false))
            {
                if (attribute.Scope == LifecycleScope.Test)
                {
                    after.Add(CreateLifecycleMethodDelegate(method));
                }
            }
        }

        return new LifecycleInfo
        {
            BeforeTestMethods = before,
            AfterTestMethods = after
        };
    }

    private static ParallelInfo BuildParallelInfo(Type type, MethodInfo method)
    {
        var notInParallel = method.GetCustomAttributes(typeof(NotInParallelAttribute), inherit: false).Any()
                            || type.GetCustomAttributes(typeof(NotInParallelAttribute), inherit: false).Any();

        var limit = method.GetCustomAttributes(typeof(ParallelLimitAttribute), inherit: false)
            .OfType<ParallelLimitAttribute>()
            .Select(a => a.MaxDegreeOfParallelism)
            .Cast<int?>()
            .FirstOrDefault()
            ?? type.GetCustomAttributes(typeof(ParallelLimitAttribute), inherit: false)
                .OfType<ParallelLimitAttribute>()
                .Select(a => a.MaxDegreeOfParallelism)
                .Cast<int?>()
                .FirstOrDefault();

        return new ParallelInfo
        {
            NotInParallel = notInParallel,
            ParallelLimit = limit
        };
    }

    private static IReadOnlyList<TestCaseId> BuildDependencies(MethodInfo method)
    {
        var builder = new List<TestCaseId>();
        var declaringType = method.DeclaringType;

        foreach (DependsOnAttribute attribute in method.GetCustomAttributes(typeof(DependsOnAttribute), inherit: false))
        {
            foreach (var name in attribute.MethodNames)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var dependencyId = declaringType != null
                        ? $"{declaringType.FullName}.{name}"
                        : name;
                    builder.Add(new TestCaseId(dependencyId));
                }
            }
        }

        return builder;
    }

    private static string CreateTestId(Type type, MethodInfo method)
    {
        return $"{type.FullName}.{method.Name}";
    }
}
