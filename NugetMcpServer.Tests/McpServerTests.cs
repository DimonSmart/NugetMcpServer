using Microsoft.Extensions.Logging;
using NuGetMcpServer.Services;
using Xunit.Abstractions;

namespace NugetMcpServer.Tests
{
    public class McpServerTests(ITestOutputHelper testOutput)
    {
        [Fact]
        public async Task CanListInterfacesFromMazeGeneratorPackage()
        {
            var httpClient = new HttpClient();
            var logger = new TestLogger<InterfaceLookupService>(testOutput);
            var service = new InterfaceLookupService(logger, httpClient);

            testOutput.WriteLine("Calling ListInterfaces on DimonSmart.MazeGenerator package...");
            try
            {
                var result = await service.ListInterfaces("DimonSmart.MazeGenerator");

                Assert.NotNull(result);
                Assert.Equal("DimonSmart.MazeGenerator", result.PackageId);
                Assert.NotEmpty(result.Version);
                Assert.NotEmpty(result.Interfaces);

                testOutput.WriteLine($"Found {result.Interfaces.Count} interfaces in {result.PackageId} version {result.Version}");

                // Output details about found interfaces with clear formatting
                testOutput.WriteLine("\n========== TEST OUTPUT: LIST OF INTERFACES ==========");
                testOutput.WriteLine(result.ToFormattedString());
                testOutput.WriteLine("===================================================\n");

                // Verify we found expected interfaces
                // Note: We're checking for the presence of any interface with "IMaze" prefix
                // as this is a common naming convention for maze generator interfaces
                Assert.Contains(result.Interfaces, i => i.Name.StartsWith("IMaze") || i.FullName.Contains(".IMaze"));

                // Get a specific interface definition if available
                var mazeInterface = result.Interfaces.FirstOrDefault(i => i.Name.StartsWith("IMaze"));
                if (mazeInterface != null)
                {
                    testOutput.WriteLine($"\nFetching interface definition for {mazeInterface.Name}");
                    var definition = await service.GetInterfaceDefinition(
                        "DimonSmart.MazeGenerator",
                        mazeInterface.Name,
                        result.Version);

                    testOutput.WriteLine("\n========== TEST OUTPUT: RESULT OF GetInterfaceDefinition ==========");
                    testOutput.WriteLine(definition);
                    testOutput.WriteLine("================================================================\n");

                    // Verify we got a valid interface definition
                    Assert.Contains("interface", definition);
                    // Check for properly formatted generic interface name (IMaze<T> instead of IMaze`1)
                    Assert.Contains("IMaze<", definition);
                }
            }
            catch (Exception ex)
            {
                testOutput.WriteLine($"Error occurred: {ex.Message}");
                testOutput.WriteLine(ex.StackTrace);
                throw;
            }
        }
        [Fact]
        public async Task CanGetMazeCellInterfaceDefinition()
        {
            var httpClient = new HttpClient();
            var logger = new TestLogger<InterfaceLookupService>(testOutput);
            var service = new InterfaceLookupService(logger, httpClient);

            testOutput.WriteLine("Getting ICell interface definition from DimonSmart.MazeGenerator package...");
            try
            {
                var version = await service.GetLatestVersion("DimonSmart.MazeGenerator");

                var definition = await service.GetInterfaceDefinition(
                    "DimonSmart.MazeGenerator",
                    "ICell",
                    version);

                testOutput.WriteLine("\n========== TEST OUTPUT: ICell INTERFACE DEFINITION ==========");
                testOutput.WriteLine(definition);
                testOutput.WriteLine("================================================================\n");

                // Verify we got a valid interface definition
                Assert.Contains("interface", definition);
                Assert.Contains("ICell", definition);
            }
            catch (Exception ex)
            {
                testOutput.WriteLine($"Error occurred: {ex.Message}");
                testOutput.WriteLine(ex.StackTrace);
                throw;
            }
        }

        [Fact]
        public async Task CanFindGenericInterfaceByBaseName()
        {
            var httpClient = new HttpClient();
            var logger = new TestLogger<InterfaceLookupService>(testOutput);
            var service = new InterfaceLookupService(logger, httpClient);

            testOutput.WriteLine("Getting IMaze generic interface definition using base name...");
            try
            {
                var version = await service.GetLatestVersion("DimonSmart.MazeGenerator");

                // Try to get IMaze interface (actually IMaze<T> in the package)
                var definition = await service.GetInterfaceDefinition(
                    "DimonSmart.MazeGenerator",
                    "IMaze",
                    version);

                testOutput.WriteLine("\n========== TEST OUTPUT: IMaze INTERFACE DEFINITION ==========");
                testOutput.WriteLine(definition);
                testOutput.WriteLine("================================================================\n");

                // Verify we got a valid generic interface definition
                Assert.Contains("interface", definition);
                Assert.Contains("IMaze<", definition); // Should be formatted as IMaze<T>, not IMaze`1

                // Verify we didn't get a "not found" error message
                Assert.DoesNotContain("not found in package", definition);
            }
            catch (Exception ex)
            {
                testOutput.WriteLine($"Error occurred: {ex.Message}");
                testOutput.WriteLine(ex.StackTrace);
                throw;
            }
        }
    }

    // Simple test logger implementation
    public class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");

            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception.Message}");
                _output.WriteLine(exception.StackTrace);
            }
        }
    }
}
