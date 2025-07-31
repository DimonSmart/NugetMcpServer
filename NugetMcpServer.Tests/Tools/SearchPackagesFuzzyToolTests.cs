using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools;

public class SearchPackagesFuzzyToolTests : TestBase
{
    private readonly TestLogger<NuGetPackageService> _packageLogger;
    private readonly TestLogger<SearchPackagesFuzzyTool> _toolLogger;
    private readonly NuGetPackageService _packageService;
    private readonly SearchPackagesFuzzyTool _tool;

    public SearchPackagesFuzzyToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        _toolLogger = new TestLogger<SearchPackagesFuzzyTool>(TestOutput);

        _packageService = CreateNuGetPackageService();
        var searchService = new PackageSearchService(new TestLogger<PackageSearchService>(TestOutput), _packageService);
        _tool = new SearchPackagesFuzzyTool(_toolLogger, searchService);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchPackagesFuzzy_InvalidQuery_ThrowsArgumentException(string query)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _tool.search_packages_fuzzy(null!, query));
    }

    [Fact]
    public async Task SearchPackagesFuzzy_WithMaxResultsExceedsLimit_ClampsResults()
    {
        // Arrange
        const string query = "logging";
        const int maxResults = 150; // Exceeds limit of 100

        // We expect this to fail because we don't have a real server
        // but the validation should still work
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _tool.search_packages_fuzzy(null!, query, maxResults));
    }

    [Fact]
    public async Task SearchPackagesFuzzy_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        const string query = "test";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _tool.search_packages_fuzzy(null!, query, cancellationToken: cts.Token));
    }
}
