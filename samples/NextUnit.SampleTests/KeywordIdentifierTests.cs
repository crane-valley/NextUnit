// Every C# keyword is lowercase, so a keyword-named type cannot satisfy the PascalCase naming rule.
// The escaped spelling is the subject of this file rather than an oversight.
#pragma warning disable IDE1006 // Naming rule violation

namespace NextUnit.SampleTests;

/// <summary>
/// Regression guard for types whose identifier is a C# keyword. Not a style recommendation:
/// the generated registry is compiled as part of this project, so an unescaped type name would
/// fail the build here rather than in a test assertion.
/// </summary>
public class KeywordIdentifierTests
{
    /// <summary>
    /// A parameter type whose identifier is a keyword.
    /// </summary>
    public sealed class @event
    {
        public int Value { get; init; }
    }

    /// <summary>
    /// A <c>ClassDataSource</c> type whose identifier is a keyword.
    /// </summary>
    public sealed class @return : IEnumerable<object?[]>
    {
        public IEnumerator<object?[]> GetEnumerator()
        {
            yield return [new @event { Value = 1 }];
            yield return [new @event { Value = 2 }];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A <c>TestData</c> member owner whose identifier is a keyword.
    /// </summary>
    public static class @static
    {
        public static IEnumerable<object?[]> Rows()
        {
            yield return [new @event { Value = 3 }];
        }
    }

    [Test]
    [ClassDataSource<@return>]
    public void ClassDataSource_WithKeywordNamedTypes(@event value)
    {
        Assert.True(value.Value > 0);
    }

    [Test]
    [TestData(nameof(@static.Rows), MemberType = typeof(@static))]
    public void TestData_WithKeywordNamedMemberType(@event value)
    {
        Assert.Equal(3, value.Value);
    }
}

/// <summary>
/// A test class whose own identifier is a keyword. Declared at namespace scope so the emitted
/// name carries the escaped identifier in the middle of a fully qualified name.
/// </summary>
public class @class
{
    [Test]
    public void KeywordNamedTestClass_Runs()
    {
        Assert.True(true);
    }
}
