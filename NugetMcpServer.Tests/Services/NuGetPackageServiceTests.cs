using System.Reflection;

using NuGetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;

using Xunit.Abstractions;

using static NuGetMcpServer.Extensions.ProgressNotifier;

namespace NuGetMcpServer.Tests.Services
{
    public class NuGetPackageServiceTests : TestBase
    {
        private readonly TestLogger<NuGetPackageService> _packageLogger;
        private readonly NuGetPackageService _packageService;

        public NuGetPackageServiceTests(ITestOutputHelper testOutput) : base(testOutput)
        {
            _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
            _packageService = CreateNuGetPackageService();
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
            using var packageStream = await _packageService.DownloadPackageAsync(packageId, version, VoidProgressNotifier);

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

        [Fact]
        public async Task SearchPackagesAsync_WithValidQuery_ReturnsResults()
        {
            // Search for a common package type
            var query = "json";

            var results = await _packageService.SearchPackagesAsync(query, 5);

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.True(results.Count <= 5);

            foreach (var package in results)
            {
                Assert.NotEmpty(package.PackageId);
                Assert.NotEmpty(package.Version);
                Assert.True(package.DownloadCount >= 0);
            }

            TestOutput.WriteLine($"Found {results.Count} packages for query '{query}':");
            foreach (var package in results.Take(3))
            {
                TestOutput.WriteLine($"- {package.PackageId} v{package.Version} ({package.DownloadCount:N0} downloads)");
            }
        }

        [Fact]
        public async Task SearchPackagesAsync_WithEmptyQuery_ReturnsEmptyList()
        {
            var results = await _packageService.SearchPackagesAsync("", 10);

            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchPackagesAsync_WithObscureQuery_MayReturnEmptyResults()
        {
            // Use a very specific query that likely won't match anything
            var query = "veryrarepackagenamethatdoesnotexist12345xyz";

            var results = await _packageService.SearchPackagesAsync(query, 10);

            // Assert - this should return empty results
            Assert.NotNull(results);
            TestOutput.WriteLine($"Search for obscure query '{query}' returned {results.Count} results");
        }
    }
}
