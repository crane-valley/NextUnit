using NextUnit;

namespace ClassLibrary.Sample.Tests;

/// <summary>
/// Tests for the Calculator class, demonstrating basic test scenarios.
/// </summary>
public class CalculatorTests
{
    private readonly Calculator _calculator = new();

    [Test]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        // Arrange
        double a = 5;
        double b = 3;

        // Act
        double result = _calculator.Add(a, b);

        // Assert
        Assert.Equal(8, result);
    }

    [Test]
    public void Add_PositiveAndNegative_ReturnsCorrectResult()
    {
        double result = _calculator.Add(10, -3);
        Assert.Equal(7, result);
    }

    [Test]
    public void Subtract_TwoNumbers_ReturnsDifference()
    {
        double result = _calculator.Subtract(10, 3);
        Assert.Equal(7, result);
    }

    [Test]
    public void Multiply_TwoNumbers_ReturnsProduct()
    {
        double result = _calculator.Multiply(4, 5);
        Assert.Equal(20, result);
    }

    [Test]
    public void Divide_ValidNumbers_ReturnsQuotient()
    {
        double result = _calculator.Divide(10, 2);
        Assert.Equal(5, result);
    }

    [Test]
    public void Divide_ByZero_ThrowsDivideByZeroException()
    {
        var ex = Assert.Throws<DivideByZeroException>(() => _calculator.Divide(10, 0));
        Assert.Contains("Cannot divide by zero", ex.Message);
    }

    [Test]
    public void Power_ValidInput_ReturnsCorrectResult()
    {
        double result = _calculator.Power(2, 3);
        Assert.Equal(8, result);
    }

    [Test]
    public void SquareRoot_PositiveNumber_ReturnsCorrectResult()
    {
        double result = _calculator.SquareRoot(16);
        Assert.Equal(4, result);
    }

    [Test]
    public void SquareRoot_NegativeNumber_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => _calculator.SquareRoot(-1));
        Assert.Contains("Cannot calculate square root of negative number", ex.Message);
    }

    [Test]
    public void Divide_SmallNumbers_UsesPrecisionComparison()
    {
        // Demonstrate approximate equality for floating-point operations
        double result = _calculator.Divide(1, 3);
        Assert.Equal(0.333333, result, precision: 5); // Compare with 5 decimal places
    }
}
