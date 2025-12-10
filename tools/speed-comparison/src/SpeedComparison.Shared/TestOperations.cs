namespace SpeedComparison.Shared;

/// <summary>
/// Common test operations shared across all frameworks.
/// Ensures identical test logic for fair comparison.
/// </summary>
public static class TestOperations
{
    /// <summary>
    /// Performs addition operation
    /// </summary>
    public static int Add(int a, int b) => a + b;

    /// <summary>
    /// Performs subtraction operation
    /// </summary>
    public static int Subtract(int a, int b) => a - b;

    /// <summary>
    /// Performs multiplication operation
    /// </summary>
    public static int Multiply(int a, int b) => a * b;

    /// <summary>
    /// Checks if number is in range
    /// </summary>
    public static bool IsInRange(int value, int min, int max) => value >= min && value <= max;

    /// <summary>
    /// Gets string length
    /// </summary>
    public static int GetLength(string text) => text.Length;

    /// <summary>
    /// Simulates async operation
    /// </summary>
    public static async Task<int> GetValueAsync()
    {
        await Task.Delay(SharedTestData.AsyncDelayMs);
        return 42;
    }

    /// <summary>
    /// Simulates async string operation
    /// </summary>
    public static async Task<string> GetStringAsync()
    {
        await Task.Delay(SharedTestData.AsyncDelayMs);
        return "result";
    }

    /// <summary>
    /// Simulates lifecycle setup operation
    /// </summary>
    public static void PerformSetup()
    {
        // Simulate setup work
        _ = DateTime.UtcNow;
    }

    /// <summary>
    /// Simulates lifecycle cleanup operation
    /// </summary>
    public static void PerformCleanup()
    {
        // Simulate cleanup work
        _ = DateTime.UtcNow;
    }
}
