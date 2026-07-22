param(
    [string]$Framework = "net10.0",
    [string]$RuntimeIdentifier
)

if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $os = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        "win"
    } elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) {
        "linux"
    } elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
        "osx"
    } else {
        throw "Unsupported operating system. Pass -RuntimeIdentifier explicitly."
    }

    $architecture = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        ([System.Runtime.InteropServices.Architecture]::X64) { "x64" }
        ([System.Runtime.InteropServices.Architecture]::X86) { "x86" }
        ([System.Runtime.InteropServices.Architecture]::Arm64) { "arm64" }
        ([System.Runtime.InteropServices.Architecture]::Arm) { "arm" }
        default { throw "Unsupported architecture. Pass -RuntimeIdentifier explicitly." }
    }
    $RuntimeIdentifier = "$os-$architecture"
}

foreach ($testFramework in @("NEXTUNIT", "TUNIT")) {
    Write-Host "Publishing $testFramework Native AOT for $Framework/$RuntimeIdentifier..."
    dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=$testFramework -p:Aot=true --framework $Framework --runtime $RuntimeIdentifier
    if ($LASTEXITCODE -ne 0) {
        throw "$testFramework Native AOT publish failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Native AOT publishes complete under UnifiedTests/bin/Release-*/$Framework/$RuntimeIdentifier/publish/."
