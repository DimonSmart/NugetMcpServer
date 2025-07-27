using System;
using System.Diagnostics;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Integration;

public class McpServerIntegrationTests(ITestOutputHelper testOutput) : TestBase(testOutput), IDisposable
{
    private Process? _serverProcess;

    public void Dispose() => StopServerProcess();

    private void StopServerProcess()
    {
        if (_serverProcess == null || _serverProcess.HasExited)
            return;

        ExecuteWithErrorHandling(
            () =>
            {
                TestOutput.WriteLine("Shutting down server process...");
                _serverProcess.Kill();
                _serverProcess.Dispose();
                _serverProcess = null;
            },
            ex => TestOutput.WriteLine($"Error shutting down server process: {ex.Message}")
        );
    }

    private async Task<Process> StartMcpServerProcess()
    {
        // Find the server executable path
        var serverDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..",
            "..", "NugetMcpServer", "bin", "Debug", "net9.0", "win-x64");

        var serverExecutablePath = Path.Combine(serverDirectory, "NugetMcpServer.exe");

        // Ensure the path exists
        if (!File.Exists(serverExecutablePath))
        {
            TestOutput.WriteLine($"Could not find server at {serverExecutablePath}");
            throw new FileNotFoundException($"Server executable not found at {serverExecutablePath}");
        }

        TestOutput.WriteLine($"Starting MCP server from: {serverExecutablePath}");

        // Start the MCP server process
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = serverExecutablePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // Capture and log stderr output
        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                TestOutput.WriteLine($"SERVER ERROR: {e.Data}");
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        // Wait a moment for the process to initialize
        await Task.Delay(1000);

        return process;
    }

    [Fact]
    public async Task CanExecuteMcpServerAndCheckForInterfaces()
    {
        if (!OperatingSystem.IsWindows())
        {
            TestOutput.WriteLine("Skipping integration test on non-Windows platform");
            return;
        }

        // Start NuGet MCP server process - this verifies the server can start
        var serverProcess = await StartMcpServerProcess();
        _serverProcess = serverProcess;

        await ExecuteWithCleanupAsync(
            TestInterfacesDirectly,
            StopServerProcess
        );
    }

    private async Task TestInterfacesDirectly()
    {
        TestOutput.WriteLine("MCP server process started, testing interfaces directly...");

        var packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        var toolLogger = new TestLogger<ListInterfacesTool>(TestOutput);

        var packageService = CreateNuGetPackageService();
        var archiveProcessingService = CreateArchiveProcessingService();
        var listTool = new ListInterfacesTool(toolLogger, packageService, archiveProcessingService);

        // Call the tool directly to verify the package contains interfaces
        var result = await listTool.list_interfaces("DimonSmart.MazeGenerator");

        // Make sure we found interfaces
        Assert.NotNull(result);
        Assert.Equal("DimonSmart.MazeGenerator", result.PackageId);
        Assert.NotEmpty(result.Interfaces);

        TestOutput.WriteLine($"Found {result.Interfaces.Count} interfaces in {result.PackageId} version {result.Version}");

        // Display the interfaces
        foreach (var iface in result.Interfaces)
        {
            TestOutput.WriteLine($"- {iface.FullName} ({iface.AssemblyName})");
        }

        // Verify that we found at least one IMaze interface
        Assert.Contains(result.Interfaces, i => i.Name.StartsWith("IMaze") || i.FullName.Contains(".IMaze"));
    }
}
