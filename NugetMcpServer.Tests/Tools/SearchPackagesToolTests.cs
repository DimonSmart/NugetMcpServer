using NuGetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools;

public class SearchPackagesToolTests : TestBase
{
    private readonly TestLogger<NuGetPackageService> _packageLogger;
    private readonly TestLogger<SearchPackagesTool> _toolLogger;
    private readonly NuGetPackageService _packageService;
    private readonly SearchPackagesTool _tool;

    public SearchPackagesToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        _toolLogger = new TestLogger<SearchPackagesTool>(TestOutput);

        _packageService = CreateNuGetPackageService();
        var searchService = new PackageSearchService(new TestLogger<PackageSearchService>(TestOutput), _packageService);
        _tool = new SearchPackagesTool(_toolLogger, searchService);
    }

    [Fact]
    public async Task SearchPackages_WithValidQuery_ReturnsResults()
    {
        // Arrange
        const string query = "Newtonsoft.Json";
        const int maxResults = 5;

        // Act
        var result = await _tool.search_packages(query, maxResults);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
        Assert.True(result.TotalCount > 0);
        Assert.NotEmpty(result.Packages);
        Assert.True(result.Packages.Count <= maxResults * 2); // Allow for some flexibility due to multiple searches
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchPackages_InvalidQuery_ThrowsArgumentException(string query)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _tool.search_packages(query));
    }

    [Fact]
    public async Task SearchPackages_WithCommaSeparatedKeywords_ReturnsResults()
    {
        // Arrange
        const string query = "json,http";
        const int maxResults = 10;

        // Act
        var result = await _tool.search_packages(query, maxResults);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
        Assert.True(result.TotalCount > 0);
        Assert.NotEmpty(result.Packages);
    }

    [Fact]
    public async Task SearchPackages_WithMaxResultsExceedsLimit_ReturnsResults()
    {
        // Arrange
        const string query = "System";
        const int maxResults = 150; // Exceeds limit of 100

        // Act
        var result = await _tool.search_packages(query, maxResults);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalCount > 0);
        Assert.NotEmpty(result.Packages);
    }

    [Fact]
    public async Task SearchPackages_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        const string query = "test";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _tool.search_packages(query, cancellationToken: cts.Token));
    }
}
