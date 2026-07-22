@echo off
setlocal

set "TARGET_RID=%~1"
if "%TARGET_RID%"=="" set "TARGET_RID=win-x64"

echo Publishing NextUnit Native AOT for net10.0/%TARGET_RID%...
dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:Aot=true --framework net10.0 --runtime %TARGET_RID%
if errorlevel 1 exit /b %errorlevel%

echo Publishing TUnit Native AOT for net10.0/%TARGET_RID%...
dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=TUNIT -p:Aot=true --framework net10.0 --runtime %TARGET_RID%
if errorlevel 1 exit /b %errorlevel%

echo Native AOT publishes complete under UnifiedTests/bin/Release-*/net10.0/%TARGET_RID%/publish/.
