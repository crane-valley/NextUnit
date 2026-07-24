using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

public sealed class ArgumentConverterTests
{
    [Fact]
    public void Convert_BoxedIntToDouble_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<double>(1, "value", "Run");

        Assert.Equal(1d, result);
    }

    [Fact]
    public void Convert_BoxedIntToLong_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<long>(1, "value", "Run");

        Assert.Equal(1L, result);
    }

    [Fact]
    public void Convert_BoxedIntToDecimal_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<decimal>(1, "value", "Run");

        Assert.Equal(1m, result);
    }

    [Fact]
    public void Convert_BoxedIntToFloat_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<float>(1, "value", "Run");

        Assert.Equal(1f, result);
    }

    [Fact]
    public void Convert_BoxedIntToNativeInt_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<nint>(1, "value", "Run");

        Assert.Equal((nint)1, result);
    }

    [Fact]
    public void Convert_BoxedNativeIntToLong_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<long>((nint)1, "value", "Run");

        Assert.Equal(1L, result);
    }

    [Fact]
    public void Convert_BoxedByteToNativeUInt_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<nuint>((byte)1, "value", "Run");

        Assert.Equal((nuint)1, result);
    }

    [Fact]
    public void Convert_BoxedNativeUIntToDecimal_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<decimal>((nuint)1, "value", "Run");

        Assert.Equal(1m, result);
    }

    [Fact]
    public void Convert_NullToNullableValueType_ReturnsNull()
    {
        var result = ArgumentConverter.Convert<int?>(null, "value", "Run");

        Assert.Null(result);
    }

    [Fact]
    public void Convert_BoxedIntToNullableDouble_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<double?>(1, "value", "Run");

        Assert.Equal(1d, result);
    }

    [Fact]
    public void Convert_BoxedIntToNullableNativeInt_ReturnsConvertedValue()
    {
        var result = ArgumentConverter.Convert<nint?>(1, "value", "Run");

        Assert.Equal((nint)1, result);
    }

    [Fact]
    public void Convert_NullToReferenceType_ReturnsNull()
    {
        var result = ArgumentConverter.Convert<string?>(null, "value", "Run");

        Assert.Null(result);
    }

    [Fact]
    public void Convert_ExactReferenceType_ReturnsSameInstance()
    {
        var value = new string('x', 1);

        var result = ArgumentConverter.Convert<string>(value, "value", "Run");

        Xunit.Assert.Same(value, result);
    }

    [Fact]
    public void Convert_ExactValueType_ReturnsSameValue()
    {
        var result = ArgumentConverter.Convert<int>(42, "value", "Run");

        Assert.Equal(42, result);
    }

    [Fact]
    public void Convert_NarrowingConversion_ThrowsDescriptiveException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ArgumentConverter.Convert<int>(1L, "count", "Run"));

        Xunit.Assert.Contains("count", exception.Message);
        Xunit.Assert.Contains("Run", exception.Message);
        Xunit.Assert.Contains("System.Int64", exception.Message);
        Xunit.Assert.Contains("System.Int32", exception.Message);
    }

    [Fact]
    public void Convert_NativeIntToInt_ThrowsDescriptiveException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ArgumentConverter.Convert<int>((nint)1, "count", "Run"));

        Xunit.Assert.Contains("count", exception.Message);
        Xunit.Assert.Contains("Run", exception.Message);
        Xunit.Assert.Contains("System.IntPtr", exception.Message);
        Xunit.Assert.Contains("System.Int32", exception.Message);
    }

    [Fact]
    public void Convert_IncompatibleType_ThrowsDescriptiveException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => ArgumentConverter.Convert<int>("one", "count", "Run"));

        Xunit.Assert.Contains("count", exception.Message);
        Xunit.Assert.Contains("Run", exception.Message);
        Xunit.Assert.Contains("System.String", exception.Message);
        Xunit.Assert.Contains("System.Int32", exception.Message);
    }
}
