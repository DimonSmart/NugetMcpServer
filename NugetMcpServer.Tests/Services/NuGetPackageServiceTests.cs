using NugetMcpServer.Tests.Helpers;
using NuGetMcpServer.Services;
using System.IO;
using System.Reflection;
using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Services
{
    public class NuGetPackageServiceTests : TestBase
    {
        private readonly TestLogger<NuGetPackageService> _packageLogger;
        private readonly NuGetPackageService _packageService;

        public NuGetPackageServiceTests(ITestOutputHelper testOutput) : base(testOutput)
        {
            _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
            _packageService = new NuGetPackageService(_packageLogger, HttpClient);
        }

        [Fact]
        public async Task GetLatestVersion_ReturnsValidVersion()
        {
            // Test with a known package
            var packageId = "DimonSmart.MazeGenerator";

            var version = await _packageService.GetLatestVersion(packageId);

            // Assert
            Assert.NotNull(version);
            Assert.NotEmpty(version);
            TestOutput.WriteLine($"Latest version for {packageId}: {version}");
        }

        [Fact]
        public async Task DownloadPackageAsync_ReturnsValidPackage()
        {
            // Test with a known package
            var packageId = "DimonSmart.MazeGenerator";
            var version = await _packageService.GetLatestVersion(packageId);

            // Download the package
            using var packageStream = await _packageService.DownloadPackageAsync(packageId, version);

            // Assert
            Assert.NotNull(packageStream);
            Assert.True(packageStream.Length > 0);
            TestOutput.WriteLine($"Downloaded {packageId} version {version}, size: {packageStream.Length} bytes");
        }

        [Fact]
        public void LoadAssemblyFromMemory_WithValidAssembly_ReturnsAssembly()
        {
            // Get a sample assembly bytes
            var currentAssembly = Assembly.GetExecutingAssembly();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(File.ReadAllBytes(currentAssembly.Location));
            stream.Position = 0;

            // Test loading from memory
            var loadedAssembly = _packageService.LoadAssemblyFromMemory(stream.ToArray());

            // Assert
            Assert.NotNull(loadedAssembly);
            TestOutput.WriteLine($"Successfully loaded assembly: {loadedAssembly.GetName().Name}");
        }
    }
}
