# dotnet test Support for NextUnit

## Overview

NextUnit uses Microsoft.Testing.Platform, which provides modern test infrastructure for .NET. Starting with .NET 10 SDK, the traditional VSTest-based `dotnet test` experience has changed.

## Quick Start (Recommended)

The recommended way to run NextUnit tests is with `dotnet run`:

```bash
dotnet run --project YourTestProject/YourTestProject.csproj
```

This works out of the box and provides full access to all NextUnit features.

## Using dotnet test (Optional)

If you prefer to use `dotnet test`, you need to opt-in to the new test experience on .NET 10 SDK and later.

### Prerequisites

1. Your test project must have a `Program.cs` file with the Microsoft.Testing.Platform entry point:

```csharp
using Microsoft.Testing.Platform.Builder;
using NextUnit.Platform;

var builder = await TestApplication.CreateBuilderAsync(args);
builder.AddNextUnit();
var app = await builder.BuildAsync();
return await app.RunAsync();
```

2. Your project file must include:

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>
```

### .NET 10 SDK Configuration

On .NET 10 SDK, you need to opt-in to the new `dotnet test` experience. Choose one of these methods:

#### Method 1: Global Configuration (Recommended)

Create or update a `global.json` file in your solution root:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  },
  "msbuild-sdks": {}
}
```

Then run `dotnet test` as usual:

```bash
dotnet test
```

#### Method 2: Environment Variable

Set the `DOTNET_CLI_USE_MSBUILD_SERVER` environment variable:

```bash
# Windows PowerShell
$env:DOTNET_CLI_USE_MSBUILD_SERVER="true"
dotnet test

# Linux/macOS
export DOTNET_CLI_USE_MSBUILD_SERVER=true
dotnet test
```

#### Method 3: Per-Project Configuration

Add to your test project's `.csproj`:

```xml
<PropertyGroup>
  <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
</PropertyGroup>
```

**Note**: This method may still require SDK-level configuration depending on your .NET SDK version.

## Troubleshooting

### Error: "testhost.dll not found"

This error indicates that `dotnet test` is trying to use the legacy VSTest infrastructure. Solutions:

1. **Use `dotnet run` instead** (recommended):
   ```bash
   dotnet run --project YourTestProject
   ```

2. **Configure .NET 10 SDK** using one of the methods above

3. **Verify your setup**:
   - Check that `<IsTestProject>true</IsTestProject>` is in your project file
   - Check that you have a `Program.cs` file with the entry point
   - Check that `<OutputType>Exe</OutputType>` is set (NextUnit.targets does this automatically)

### Error: "Testing with VSTest target is no longer supported"

This error appears on .NET 10 SDK when using `dotnet test` without opting in to the new experience. Follow the configuration steps above.

## Additional Resources

- [Microsoft.Testing.Platform Documentation](https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-intro)
- [.NET 10 SDK Test Changes](https://aka.ms/dotnet-test-mtp-error)
- [NextUnit Getting Started](./GETTING_STARTED.md)

## Why dotnet run?

Microsoft.Testing.Platform is designed as a standalone test runner that provides:
- Better performance
- More flexible output options
- Native support for modern features
- Consistent behavior across different environments

Using `dotnet run` aligns with this architecture and provides the best experience.
