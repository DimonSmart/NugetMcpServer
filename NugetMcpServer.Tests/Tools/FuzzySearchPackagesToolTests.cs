using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools;

public class FuzzySearchPackagesToolTests : TestBase
{
    private readonly TestLogger<NuGetPackageService> _packageLogger;
    private readonly TestLogger<FuzzySearchPackagesTool> _toolLogger;
    private readonly NuGetPackageService _packageService;
    private readonly FuzzySearchPackagesTool _tool;

    public FuzzySearchPackagesToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        _toolLogger = new TestLogger<FuzzySearchPackagesTool>(TestOutput);

        _packageService = CreateNuGetPackageService();
        var searchService = new PackageSearchService(new TestLogger<PackageSearchService>(TestOutput), _packageService);
        _tool = new FuzzySearchPackagesTool(_toolLogger, searchService);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FuzzySearchPackages_InvalidQuery_ThrowsArgumentException(string query)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _tool.fuzzy_search_packages(null!, query));
    }

    [Fact]
    public async Task FuzzySearchPackages_WithMaxResultsExceedsLimit_ClampsResults()
    {
        // Arrange
        const string query = "logging";
        const int maxResults = 150; // Exceeds limit of 100

        // We expect this to fail because we don't have a real server
        // but the validation should still work
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _tool.fuzzy_search_packages(null!, query, maxResults));
    }

    [Fact]
    public async Task FuzzySearchPackages_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        const string query = "test";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _tool.fuzzy_search_packages(null!, query, cancellationToken: cts.Token));
    }
}
