using NugetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Tools
{
    public class ListInterfacesToolTests : TestBase
    {
        private readonly TestLogger<NuGetPackageService> _packageLogger;
        private readonly TestLogger<ListInterfacesTool> _listToolLogger;
        private readonly NuGetPackageService _packageService;
        private readonly ListInterfacesTool _listTool;

        public ListInterfacesToolTests(ITestOutputHelper testOutput) : base(testOutput)
        {
            _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
            _listToolLogger = new TestLogger<ListInterfacesTool>(TestOutput);

            _packageService = new NuGetPackageService(_packageLogger, HttpClient);
            _listTool = new ListInterfacesTool(_listToolLogger, _packageService);
        }

        [Fact]
        public async Task ListInterfaces_WithValidPackage_ReturnsInterfaces()
        {
            // Test with a known package
            var packageId = "DimonSmart.MazeGenerator";

            var result = await _listTool.ListInterfaces(packageId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(packageId, result.PackageId);
            Assert.NotEmpty(result.Version);
            Assert.NotEmpty(result.Interfaces);

            TestOutput.WriteLine($"Found {result.Interfaces.Count} interfaces in {result.PackageId} version {result.Version}");
            TestOutput.WriteLine("\n========== TEST OUTPUT: LIST OF INTERFACES ==========");
            TestOutput.WriteLine(result.ToFormattedString());
            TestOutput.WriteLine("===================================================\n");

            // Verify we found expected interfaces
            Assert.Contains(result.Interfaces, i => i.Name.StartsWith("IMaze") || i.FullName.Contains(".IMaze"));
        }

        [Fact]
        public async Task ListInterfaces_WithSpecificVersion_ReturnsInterfaces()
        {
            // Test with a known package and version
            var packageId = "DimonSmart.MazeGenerator";
            var version = await _packageService.GetLatestVersion(packageId);

            var result = await _listTool.ListInterfaces(packageId, version);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(packageId, result.PackageId);
            Assert.Equal(version, result.Version);
            Assert.NotEmpty(result.Interfaces);

            TestOutput.WriteLine($"Found {result.Interfaces.Count} interfaces in {result.PackageId} version {result.Version}");
        }
    }
}
