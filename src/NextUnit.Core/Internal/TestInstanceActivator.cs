using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NextUnit.Core;

namespace NextUnit.Internal;

/// <summary>
/// Creates test class instances, injecting the test output and context a class asks for.
/// </summary>
/// <remarks>
/// The generator emits a factory delegate for every test class it can see, so the reflection path
/// here is the fallback for classes the generator did not process, such as those discovered through
/// a hand-built descriptor.
/// </remarks>
internal static class TestInstanceActivator
{
    /// <summary>
    /// Creates a test class instance with appropriate constructor injection.
    /// </summary>
    /// <param name="testCase">The test descriptor containing the generated factory or class type.</param>
    /// <param name="testOutput">The test output capture to inject into the constructor.</param>
    /// <param name="testContext">The test context capture to inject into the constructor.</param>
    /// <returns>A new instance of the test class.</returns>
    public static object Create(
        TestCaseDescriptor testCase,
        ITestOutput testOutput,
        ITestContext testContext)
    {
        if (testCase.TestClassFactory is not null)
        {
            return testCase.TestClassFactory(testOutput, testContext);
        }

        return CreateWithReflection(testCase.TestClass, testOutput, testContext);
    }

    private static object CreateWithReflection(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type testClass,
        ITestOutput testOutput,
        ITestContext testContext)
    {
        // Find the best matching constructor in a single pass
        // Priority: 2-param > 1-param ITestContext > 1-param ITestOutput > parameterless
        var constructors = testClass.GetConstructors();

        ConstructorInfo? twoParamCtor = null;
        bool twoParamContextFirst = false;
        ConstructorInfo? contextOnlyCtor = null;
        ConstructorInfo? outputOnlyCtor = null;
        ConstructorInfo? parameterlessCtor = null;

        foreach (var ctor in constructors)
        {
            var parameters = ctor.GetParameters();

            switch (parameters.Length)
            {
                case 0:
                    parameterlessCtor = ctor;
                    break;
                case 1:
                    if (parameters[0].ParameterType == typeof(ITestContext))
                    {
                        contextOnlyCtor = ctor;
                    }
                    else if (parameters[0].ParameterType == typeof(ITestOutput))
                    {
                        outputOnlyCtor = ctor;
                    }
                    break;
                case 2:
                    var param0Type = parameters[0].ParameterType;
                    var param1Type = parameters[1].ParameterType;
                    if (param0Type == typeof(ITestContext) && param1Type == typeof(ITestOutput))
                    {
                        twoParamCtor = ctor;
                        twoParamContextFirst = true;
                    }
                    else if (param0Type == typeof(ITestOutput) && param1Type == typeof(ITestContext))
                    {
                        twoParamCtor = ctor;
                        twoParamContextFirst = false;
                    }
                    break;
            }
        }

        // Return based on priority
        if (twoParamCtor is not null)
        {
            return twoParamContextFirst
                ? twoParamCtor.Invoke([testContext, testOutput])
                : twoParamCtor.Invoke([testOutput, testContext]);
        }

        if (contextOnlyCtor is not null)
        {
            return contextOnlyCtor.Invoke([testContext]);
        }

        if (outputOnlyCtor is not null)
        {
            return outputOnlyCtor.Invoke([testOutput]);
        }

        if (parameterlessCtor is not null)
        {
            return parameterlessCtor.Invoke([]);
        }

        return Activator.CreateInstance(testClass)!;
    }
}
