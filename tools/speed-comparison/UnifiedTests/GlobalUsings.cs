// Framework-specific namespaces
#if NEXTUNIT
global using NextUnit;
global using NextUnit.Core;
#elif XUNIT
global using Xunit;
#elif NUNIT
global using NUnit.Framework;
#elif MSTEST
global using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

// Unified attribute aliases for cross-framework compatibility
#if NEXTUNIT
global using TestAttribute = NextUnit.TestAttribute;
global using DataDrivenTestAttribute = NextUnit.TestAttribute;
global using TestDataAttribute = NextUnit.ArgumentsAttribute;
global using TestDataSourceAttribute = NextUnit.TestDataAttribute;
#elif XUNIT
// xUnit uses Fact for simple tests, Theory for parameterized tests
global using TestAttribute = Xunit.FactAttribute;
global using DataDrivenTestAttribute = Xunit.TheoryAttribute;
global using TestDataAttribute = Xunit.InlineDataAttribute;
global using TestDataSourceAttribute = Xunit.MemberDataAttribute;
#elif NUNIT
global using TestAttribute = NUnit.Framework.TestAttribute;
global using DataDrivenTestAttribute = NUnit.Framework.TestAttribute;
global using TestDataAttribute = NUnit.Framework.TestCaseAttribute;
global using TestDataSourceAttribute = NUnit.Framework.TestCaseSourceAttribute;
global using TestClassAttribute = NUnit.Framework.TestFixtureAttribute;
#elif MSTEST
global using TestAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
global using DataDrivenTestAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute;
global using TestDataAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.DataRowAttribute;
global using TestDataSourceAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.DynamicDataAttribute;
global using TestClassAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
#endif

// Empty attribute for frameworks that don't require class-level attributes
#if NEXTUNIT || XUNIT
[System.AttributeUsage(System.AttributeTargets.Class)]
internal class TestClassAttribute : System.Attribute { }
#endif
