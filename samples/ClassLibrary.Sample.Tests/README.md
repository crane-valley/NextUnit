# Class Library Testing Sample

This sample demonstrates how to use **NextUnit** to test a .NET class library. It showcases common testing patterns and NextUnit features for testing business logic.

## Project Structure

```
ClassLibrary.Sample/          # The class library being tested
├── Calculator.cs             # Basic arithmetic operations
├── StringHelpers.cs          # String manipulation utilities
└── OrderProcessor.cs         # E-commerce order processing logic

ClassLibrary.Sample.Tests/    # NextUnit test project
├── CalculatorTests.cs        # Tests for Calculator class
├── StringHelpersTests.cs     # Tests for StringHelpers class
└── OrderProcessorTests.cs    # Tests for OrderProcessor class
```

## Features Demonstrated

### 1. Basic Test Structure

```csharp
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
```

### 2. Exception Testing

```csharp
[Test]
public void Divide_ByZero_ThrowsDivideByZeroException()
{
    var ex = Assert.Throws<DivideByZeroException>(() => _calculator.Divide(10, 0));
    Assert.Contains("Cannot divide by zero", ex.Message);
}
```

### 3. Floating-Point Precision Comparisons

```csharp
[Test]
public void Divide_SmallNumbers_UsesPrecisionComparison()
{
    double result = _calculator.Divide(1, 3);
    Assert.Equal(0.333333, result, precision: 5); // Compare with 5 decimal places
}
```

### 4. Parameterized Tests with TestData

```csharp
[TestData("hello", "olleh")]
[TestData("NextUnit", "tinUtxeN")]
[TestData("a", "a")]
public void Reverse_VariousInputs_ReturnsExpectedOutput(string input, string expected)
{
    string result = StringHelpers.Reverse(input);
    Assert.Equal(expected, result);
}
```

### 5. Business Logic Testing

The `OrderProcessorTests` class demonstrates:
- Validating complex business rules
- Testing object properties and calculations
- Testing conditional logic (coupons, shipping)
- Testing collections and aggregations

### 6. Collection Assertions

```csharp
[Test]
public void ValidateOrder_ValidOrder_ReturnsValid()
{
    var result = _processor.ValidateOrder(order);
    
    Assert.True(result.IsValid);
    Assert.Empty(result.Errors);
}
```

## Running the Tests

### Build and Run All Tests

```bash
# From the ClassLibrary.Sample.Tests directory
dotnet test
```

### Run Specific Tests

```bash
# Run only Calculator tests
dotnet test --test-name "*CalculatorTests*"

# Run only tests with "Coupon" in the name
dotnet test --test-name "*Coupon*"
```

### Run with Filters

```bash
# Run tests by category (if you add [Category("Fast")] attributes)
dotnet test --category Fast

# Exclude slow tests
dotnet test --exclude-category Slow
```

## Project Setup

### 1. Create the Class Library

```bash
dotnet new classlib -n ClassLibrary.Sample -f net10.0
```

### 2. Create the Test Project

```bash
dotnet new console -n ClassLibrary.Sample.Tests -f net10.0
```

### 3. Update Test Project Configuration

Edit `ClassLibrary.Sample.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../ClassLibrary.Sample/ClassLibrary.Sample.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="NextUnit" />
    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  </ItemGroup>
</Project>
```

### 4. Add NextUnit Packages

The NextUnit packages are defined in the parent `Directory.Packages.props` file using Central Package Management. If you're creating a standalone project, add version numbers:

```xml
<PackageReference Include="NextUnit" Version="1.6.6" />
```

## Test Naming Conventions

This sample follows the pattern: `MethodName_Scenario_ExpectedResult`

Examples:
- `Add_TwoPositiveNumbers_ReturnsSum`
- `Divide_ByZero_ThrowsDivideByZeroException`
- `ApplyCoupon_ValidCoupon_AppliesDiscount`

This makes tests self-documenting and easy to understand.

## Best Practices Demonstrated

1. **Arrange-Act-Assert (AAA) Pattern**: Tests are organized into three clear sections
2. **Single Responsibility**: Each test validates one behavior
3. **Descriptive Names**: Test names clearly describe what is being tested
4. **Setup Reuse**: Use readonly fields for common test fixtures
5. **Parameterized Tests**: Use `[TestData]` for testing multiple inputs
6. **Exception Testing**: Use `Assert.Throws<T>()` for exception scenarios
7. **Precision Comparisons**: Use `precision` parameter for floating-point tests

## Common NextUnit Assertions Used

| Assertion | Purpose | Example |
|-----------|---------|---------|
| `Assert.Equal(expected, actual)` | Equality comparison | `Assert.Equal(8, result)` |
| `Assert.Equal(expected, actual, precision)` | Floating-point equality | `Assert.Equal(0.333, result, 3)` |
| `Assert.True(condition)` | Boolean true | `Assert.True(result.IsValid)` |
| `Assert.False(condition)` | Boolean false | `Assert.False(result.HasErrors)` |
| `Assert.Null(value)` | Null check | `Assert.Null(order.CouponCode)` |
| `Assert.Empty(collection)` | Empty collection | `Assert.Empty(result.Errors)` |
| `Assert.Contains(expected, actual)` | String/collection contains | `Assert.Contains("error", message)` |
| `Assert.Throws<T>(action)` | Exception throwing | `Assert.Throws<ArgumentException>(...)` |

## Learn More

- [NextUnit Documentation](../../docs/GETTING_STARTED.md)
- [Best Practices Guide](../../docs/BEST_PRACTICES.md)
- [Migration from xUnit](../../docs/MIGRATION_FROM_XUNIT.md)

## Related Samples

- [Console Application Testing](../Console.Sample.Tests/) - Testing console apps
- [NextUnit Feature Showcase](../NextUnit.SampleTests/) - Comprehensive feature demonstrations
