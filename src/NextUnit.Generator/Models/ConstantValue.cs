namespace NextUnit.Generator.Models;

/// <summary>
/// A compile-time constant resolved to the text the generator needs, so the pipeline models never
/// hold a <c>TypedConstant</c> (which roots the compilation through its type symbol).
/// </summary>
internal sealed record ConstantValue
{
    public ConstantValue(
        string codeLiteral,
        string displayLiteral,
        string equalityKey,
        bool isNull)
    {
        CodeLiteral = codeLiteral;
        DisplayLiteral = displayLiteral;
        EqualityKey = equalityKey;
        IsNull = isNull;
    }

    /// <summary>
    /// Gets the value rendered as a C# expression for the generated source.
    /// </summary>
    public string CodeLiteral { get; }

    /// <summary>
    /// Gets the value rendered for test display names.
    /// </summary>
    public string DisplayLiteral { get; }

    /// <summary>
    /// Gets the key used to match matrix values against matrix exclusions.
    /// </summary>
    /// <remarks>
    /// Mirrors the value-based comparison the exclusion matcher used to run against
    /// <c>TypedConstant.Value</c>: the runtime type of the boxed value participates, so an
    /// <see cref="int"/> and a <see cref="long"/> holding the same number stay distinct.
    /// </remarks>
    public string EqualityKey { get; }

    /// <summary>
    /// Gets a value indicating whether the constant is <c>null</c>.
    /// </summary>
    public bool IsNull { get; }
}

/// <summary>
/// A test method parameter resolved to the text the generator needs, so the pipeline models never
/// hold an <c>IParameterSymbol</c>.
/// </summary>
internal sealed record ParameterDescriptor
{
    public ParameterDescriptor(
        string name,
        string typeofName,
        string fullyQualifiedTypeName,
        string displayTypeName,
        bool isValueType)
    {
        Name = name;
        TypeofName = typeofName;
        FullyQualifiedTypeName = fullyQualifiedTypeName;
        DisplayTypeName = displayTypeName;
        IsValueType = isValueType;
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter type formatted for <c>typeof()</c> expressions.
    /// </summary>
    public string TypeofName { get; }

    /// <summary>
    /// Gets the fully qualified parameter type name.
    /// </summary>
    public string FullyQualifiedTypeName { get; }

    /// <summary>
    /// Gets the parameter type in the default display format.
    /// </summary>
    public string DisplayTypeName { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter type is a value type.
    /// </summary>
    public bool IsValueType { get; }
}
