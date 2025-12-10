@echo off
REM Script to prepare AOT builds for benchmarking

echo Building NextUnit AOT version for net10.0...
dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true --framework net10.0

echo AOT build complete. Output in: UnifiedTests/bin/Release-NEXTUNIT/net10.0/publish/
