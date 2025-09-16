@echo off
REM Batch script for building and packaging NugetMcpServer

echo Building and packaging NugetMcpServer...

echo Synchronizing version across project files...
powershell -ExecutionPolicy Bypass -File sync-version.ps1

cd NugetMcpServer

echo Cleaning previous builds...
dotnet clean -c Release

echo Restoring dependencies...
dotnet restore

echo Building project...
dotnet build -c Release --no-restore

echo Creating NuGet package...
dotnet pack -c Release --no-build

echo.
echo Package created in: bin\Release\
dir bin\Release\*.nupkg

echo.
echo Build and packaging completed successfully!
pause