namespace ClassLibrary.Sample;

/// <summary>
/// A simple calculator class demonstrating basic arithmetic operations.
/// </summary>
public class Calculator
{
    /// <summary>
    /// Adds two numbers together.
    /// </summary>
    public double Add(double a, double b) => a + b;

    /// <summary>
    /// Subtracts b from a.
    /// </summary>
    public double Subtract(double a, double b) => a - b;

    /// <summary>
    /// Multiplies two numbers.
    /// </summary>
    public double Multiply(double a, double b) => a * b;

    /// <summary>
    /// Divides a by b.
    /// </summary>
    /// <exception cref="DivideByZeroException">Thrown when b is zero.</exception>
    public double Divide(double a, double b)
    {
        if (b.Equals(0d))
        {
            throw new DivideByZeroException("Cannot divide by zero");
        }

        return a / b;
    }

    /// <summary>
    /// Calculates the power of a number.
    /// </summary>
    public double Power(double baseNumber, double exponent) => Math.Pow(baseNumber, exponent);

    /// <summary>
    /// Calculates the square root of a number.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when number is negative.</exception>
    public double SquareRoot(double number)
    {
        if (number < 0)
        {
            throw new ArgumentException(
                "Cannot calculate square root of negative number",
                nameof(number));
        }

        return Math.Sqrt(number);
    }
}
