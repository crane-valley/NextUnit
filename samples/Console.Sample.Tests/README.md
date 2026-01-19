# Console Application Testing Sample

This sample demonstrates how to use **NextUnit** to test a .NET console application.
It showcases testing command-line argument parsing, file processing logic,
and other console application patterns.

## Project Structure

```text
Console.Sample/                # The console application being tested
├── Program.cs                 # Main entry point
├── ArgumentParser.cs          # Command-line argument parsing
└── FileProcessor.cs           # File analysis and text processing

Console.Sample.Tests/          # NextUnit test project
├── ArgumentParserTests.cs     # Tests for command-line parsing
└── FileProcessorTests.cs      # Tests for file processing logic
```

## Console Application Features

The sample console application analyzes text files and provides statistics:

- Line count, word count, character count
- Search functionality with case-sensitive/insensitive options
- Line filtering capabilities
- Command-line argument parsing with flags and options

## Features Demonstrated

### 1. Testing Command-Line Argument Parsing

```csharp
[Test]
public void Parse_HelpFlag_SetsShowHelp()
{
    var parser = new ArgumentParser();
    var result = parser.Parse(new[] { "--help" });
    
    Assert.True(result.ShowHelp);
}
```

### 2. Testing File Processing Logic

```csharp
[Test]
public void AnalyzeFile_MultipleLines_ReturnsCorrectStatistics()
{
    var content = "Line one\nLine two\nLine three";
    var processor = new FileProcessor();
    
    var result = processor.AnalyzeFile(content);
    
    Assert.Equal(3, result.LineCount);
    Assert.Equal(6, result.WordCount);
}
```

### 3. Testing Search Functionality

```csharp
[Test]
public void SearchLines_CaseInsensitive_FindsAllMatches()
{
    var content = "Hello World\nhello world\nHELLO WORLD";
    
    var result = _processor.SearchLines(content, "Hello", caseSensitive: false);
    
    Assert.Equal(3, result.Count());
}
```

### 4. Testing Collection Results

```csharp
[Test]
public void FilterLines_WithPredicate_ReturnsFilteredLines()
{
    var content = "keep\nremove\nkeep\nremove";
    
    var result = _processor.FilterLines(content, line => line.StartsWith("keep"));
    
    Assert.Equal(2, result.Count());
    Assert.All(result, line => Assert.True(line.StartsWith("keep")));
}
```

### 5. Parameterized Tests for Flags

```csharp
[Test]
[TestData(nameof(FlagTestCases))]
public void Parse_VariousFlags_SetsCorrectProperty(
    string[] args, 
    string propertyName, 
    bool expectedValue)
{
    var result = _parser.Parse(args);
    var property = typeof(ParsedArguments).GetProperty(propertyName);
    var actualValue = (bool)property!.GetValue(result)!;
    
    Assert.Equal(expectedValue, actualValue);
}
```

## Running the Tests

### Build and Run All Tests

```bash
# From the Console.Sample.Tests directory
dotnet test
```

### Run the Console Application

```bash
# From the Console.Sample directory
dotnet run -- --help
dotnet run -- --verbose example.txt
dotnet run -- -o output.txt input1.txt input2.txt
```

## Test Categories

### ArgumentParserTests (11 tests)

- Empty arguments handling
- Help flag parsing (short and long forms)
- Verbose flag parsing  
- Output and format options
- Missing option values
- Unknown option errors
- Input file collection
- Mixed options parsing
- Parameterized flag variations

### FileProcessorTests (14 tests)

- Empty content handling
- Single and multi-line statistics
- Line filtering with predicates
- Case-sensitive and case-insensitive search
- Multiple match counting
- Search result validation

## Key Testing Patterns

### 1. Testing Void Methods with Side Effects

Test by verifying the state changes:

```csharp
var options = parser.Parse(new[] { "--verbose" });
Assert.True(options.Verbose);
```

### 2. Testing Collection Results

Use collection assertions:

```csharp
Assert.Empty(result);
Assert.Equal(expectedCount, result.Count());
Assert.All(result, item => /* validation */);
```

### 3. Testing Error Conditions

Verify error messages are added:

```csharp
Assert.False(result.IsValid);
Assert.Contains(result.Errors, e => e.Contains("expected error"));
```

### 4. Testing with Reflection

When testing dynamic properties:

```csharp
var property = typeof(ParsedArguments).GetProperty(propertyName);
var actualValue = property!.GetValue(result);
Assert.Equal(expectedValue, actualValue);
```

## Best Practices for Console App Testing

1. **Separate Business Logic**: Keep command-line parsing and core logic separate for easier testing
2. **Dependency Injection**: Use interfaces for file system operations
   to enable testing without actual files
3. **Test Exit Codes**: Verify the application returns correct exit codes
4. **Test Error Messages**: Ensure error messages are helpful and clear
5. **Mock External Dependencies**: Use test doubles for file I/O, network calls, etc.

## Project Setup

### 1. Create the Console Application

```bash
dotnet new console -n Console.Sample -f net10.0
```

### 2. Create the Test Project

```bash
dotnet new console -n Console.Sample.Tests -f net10.0
```

### 3. Update Test Project Configuration

Edit `Console.Sample.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../Console.Sample/Console.Sample.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="NextUnit" />
    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  </ItemGroup>
</Project>
```

### 4. Add Program.cs for Test Initialization

```csharp
using Microsoft.Testing.Platform.Builder;
using NextUnit.Platform;

var builder = await TestApplication.CreateBuilderAsync(args);
builder.AddNextUnit();
using var app = await builder.BuildAsync();
return await app.RunAsync();
```

## Advanced Testing Scenarios

### Testing with Temporary Files

```csharp
[Test]
public void ProcessFile_ValidFile_ReturnsStatistics()
{
    var tempFile = Path.GetTempFileName();
    try
    {
        File.WriteAllText(tempFile, "test content");
        
        var processor = new FileProcessor();
        var content = File.ReadAllText(tempFile);
        var stats = processor.AnalyzeFile(content);
        
        Assert.Equal(1, stats.LineCount);
    }
    finally
    {
        File.Delete(tempFile);
    }
}
```

### Testing Console Output

For testing actual console output, consider using `ITestOutput` or capturing stdout/stderr:

```csharp
// Redirect console output for testing
using var sw = new StringWriter();
Console.SetOut(sw);

// Run console code
ShowHelp();

// Verify output
var output = sw.ToString();
Assert.Contains("Usage:", output);
```

## Common NextUnit Assertions for Console Apps

| Assertion | Purpose | Example |
| --------- | ------- | ------- |
| `Assert.True(condition)` | Boolean validation | `Assert.True(options.IsValid)` |
| `Assert.Equal(expected, actual)` | Value comparison | `Assert.Equal(3, result.Count())` |
| `Assert.Empty(collection)` | Empty collection | `Assert.Empty(errors)` |
| `Assert.Contains(item, collection)` | Collection membership | `Assert.Contains("error", messages)` |
| `Assert.All(collection, predicate)` | All items match | `Assert.All(lines, l => l.Length > 0)` |

## Learn More

- [NextUnit Documentation](../../docs/GETTING_STARTED.md)
- [Best Practices Guide](../../docs/BEST_PRACTICES.md)
- [Class Library Testing Sample](../ClassLibrary.Sample.Tests/) - Basic testing patterns

## Related Samples

- [Class Library Testing](../ClassLibrary.Sample.Tests/) - Testing business logic
- [NextUnit Feature Showcase](../NextUnit.SampleTests/) - Comprehensive feature demonstrations
