# Script to prepare AOT builds for benchmarking
param(
    [string]$Framework = "net10.0"
)

Write-Host "Building NextUnit AOT version for $Framework..."
dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true --framework $Framework

Write-Host "AOT build complete. Output in: UnifiedTests/bin/Release-NEXTUNIT/$Framework/publish/"
