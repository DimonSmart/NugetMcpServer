using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;
using System.Diagnostics;
using Xunit.Abstractions;

namespace NugetMcpServer.Tests
{
    public class McpServerProcessTests(ITestOutputHelper testOutput) : IDisposable
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
                    testOutput.WriteLine("Shutting down server process...");
                    _serverProcess.Kill();
                    _serverProcess.Dispose();
                    _serverProcess = null;
                },
                ex => testOutput.WriteLine($"Error shutting down server process: {ex.Message}")
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
                testOutput.WriteLine($"Could not find server at {serverExecutablePath}");
                throw new FileNotFoundException($"Server executable not found at {serverExecutablePath}");
            }

            testOutput.WriteLine($"Starting MCP server from: {serverExecutablePath}");

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
                    testOutput.WriteLine($"SERVER ERROR: {e.Data}");
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
            testOutput.WriteLine("MCP server process started, testing interfaces directly...");

            // Use the new ListInterfacesTool class
            var httpClient = new HttpClient();
            var packageLogger = new TestLogger<NuGetPackageService>(testOutput);
            var toolLogger = new TestLogger<ListInterfacesTool>(testOutput);

            var packageService = new NuGetPackageService(packageLogger, httpClient);
            var listTool = new ListInterfacesTool(toolLogger, packageService);

            // Call the tool directly to verify the package contains interfaces
            var result = await listTool.ListInterfaces("DimonSmart.MazeGenerator");

            // Make sure we found interfaces
            Assert.NotNull(result);
            Assert.Equal("DimonSmart.MazeGenerator", result.PackageId);
            Assert.NotEmpty(result.Interfaces);

            testOutput.WriteLine($"Found {result.Interfaces.Count} interfaces in {result.PackageId} version {result.Version}");

            // Display the interfaces
            foreach (var iface in result.Interfaces)
            {
                testOutput.WriteLine($"- {iface.FullName} ({iface.AssemblyName})");
            }

            // Verify that we found at least one IMaze interface
            Assert.Contains(result.Interfaces, i => i.Name.StartsWith("IMaze") || i.FullName.Contains(".IMaze"));
        }

        #region Helper Methods

        private static void ExecuteWithErrorHandling(Action action, Action<Exception>? exceptionHandler = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exceptionHandler?.Invoke(ex);
            }
        }

        private static async Task ExecuteWithCleanupAsync(Func<Task> operation, Action cleanup)
        {
            try
            {
                await operation();
            }
            finally
            {
                cleanup();
            }
        }

        #endregion
    }
}
