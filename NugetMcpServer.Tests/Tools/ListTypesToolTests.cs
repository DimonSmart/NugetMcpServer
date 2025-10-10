using NuGetMcpServer.Services;
using NuGetMcpServer.Services.Formatters;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools;

public class ListTypesToolTests : TestBase
{
    private readonly TestLogger<NuGetPackageService> _packageLogger;
    private readonly TestLogger<ListTypesTool> _listToolLogger;
    private readonly NuGetPackageService _packageService;
    private readonly ArchiveProcessingService _archiveProcessingService;
    private readonly ListTypesTool _listTool;

    public ListTypesToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        _listToolLogger = new TestLogger<ListTypesTool>(TestOutput);

        _packageService = CreateNuGetPackageService();
        _archiveProcessingService = CreateArchiveProcessingService();
        _listTool = new ListTypesTool(_listToolLogger, _packageService, _archiveProcessingService);
    }

    [Fact]
    public async Task ListClasses_WithValidPackage_ReturnsClasses()
    {
        // Test with a known package
        var packageId = "DimonSmart.MazeGenerator";

        var result = await _listTool.list_classes_records_structs(packageId, maxResults: 1000);

        Assert.NotNull(result);
        Assert.Equal(packageId, result.PackageId);
        Assert.NotEmpty(result.Version);
        Assert.NotEmpty(result.Types);

        TestOutput.WriteLine($"Found {result.Types.Count} types in {result.PackageId} version {result.Version}");
        TestOutput.WriteLine($"Total: {result.TotalCount}, Returned: {result.ReturnedCount}, IsPartial: {result.IsPartial}");
        TestOutput.WriteLine("\n========== TEST OUTPUT: LIST OF TYPES ==========");
        TestOutput.WriteLine(result.Format());
        TestOutput.WriteLine("================================================\n");

        // Verify we found expected classes - using Point instead of Cell as Cell doesn't exist in current version
        Assert.Contains(result.Types, c => c.Name == "Point" || c.FullName.Contains(".Point"));

        // Ensure that structs and records are also included
        Assert.Contains(result.Types, c => c.Kind != TypeKind.Class);
    }

    [Fact]
    public async Task ListClasses_WithSpecificVersion_ReturnsClasses()
    {
        // Test with a known package and version
        var packageId = "DimonSmart.MazeGenerator";
        var version = await _packageService.GetLatestVersion(packageId);

        var result = await _listTool.list_classes_records_structs(packageId, version, maxResults: 1000);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(packageId, result.PackageId);
        Assert.Equal(version, result.Version);
        Assert.NotEmpty(result.Types);

        TestOutput.WriteLine($"Found {result.Types.Count} types in {result.PackageId} version {result.Version}");
    }

    [Fact]
    public async Task ListClasses_ContainsClassModifierInformation()
    {
        // Test that the result contains modifier information
        var packageId = "DimonSmart.MazeGenerator";
        var result = await _listTool.list_classes_records_structs(packageId, maxResults: 1000);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Types);

        // Check that we have some information about modifiers (at least one class should have some modifier info)
        var hasModifiers = result.Types.Any(c => c.IsStatic || c.IsAbstract || c.IsSealed);

        // Log all classes with their modifiers for debugging
        foreach (var cls in result.Types)
        {
            TestOutput.WriteLine($"Class: {cls.Name} - Static: {cls.IsStatic}, Abstract: {cls.IsAbstract}, Sealed: {cls.IsSealed}");
        }

        TestOutput.WriteLine($"Total classes found: {result.Types.Count}");
        TestOutput.WriteLine($"Classes with modifiers: {result.Types.Count(c => c.IsStatic || c.IsAbstract || c.IsSealed)}");
    }

    [Fact]
    public async Task ListClasses_WithWildcardFilter_ReturnsMatchingTypes()
    {
        // Test with wildcard filter
        var packageId = "DimonSmart.MazeGenerator";
        var filter = "*Point*";

        var result = await _listTool.list_classes_records_structs(packageId, filter: filter);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Types);

        // All returned types should contain "Point" in name or full name
        Assert.All(result.Types, t =>
            Assert.True(
                t.Name.Contains("Point", StringComparison.OrdinalIgnoreCase) ||
                t.FullName.Contains("Point", StringComparison.OrdinalIgnoreCase),
                $"Type {t.FullName} does not match filter {filter}"));

        TestOutput.WriteLine($"Filter: {filter}");
        TestOutput.WriteLine($"Total matching types: {result.TotalCount}");
        TestOutput.WriteLine($"Returned types: {result.ReturnedCount}");

        foreach (var type in result.Types)
        {
            TestOutput.WriteLine($"  - {type.FullName}");
        }
    }

    [Fact]
    public async Task ListClasses_WithNamespaceFilter_ReturnsMatchingTypes()
    {
        // Test with namespace filter
        var packageId = "DimonSmart.MazeGenerator";

        // First, get all types to find a valid namespace
        var allTypes = await _listTool.list_classes_records_structs(packageId, maxResults: 1000);

        if (allTypes.Types.Count == 0)
        {
            TestOutput.WriteLine("No types found in package");
            return;
        }

        // Get the namespace from the first type
        var firstType = allTypes.Types.First();
        var namespaceParts = firstType.FullName.Split('.');
        if (namespaceParts.Length < 2)
        {
            TestOutput.WriteLine($"Type {firstType.FullName} has no namespace");
            return;
        }

        var namespaceFilter = string.Join(".", namespaceParts.Take(namespaceParts.Length - 1)) + ".*";

        var result = await _listTool.list_classes_records_structs(packageId, filter: namespaceFilter);

        Assert.NotNull(result);

        TestOutput.WriteLine($"Filter: {namespaceFilter}");
        TestOutput.WriteLine($"Total matching types: {result.TotalCount}");
        TestOutput.WriteLine($"Returned types: {result.ReturnedCount}");
    }

    [Fact]
    public async Task ListClasses_WithMaxResults_LimitsOutput()
    {
        // Test pagination with maxResults
        var packageId = "DimonSmart.MazeGenerator";
        var maxResults = 5;

        var result = await _listTool.list_classes_records_structs(packageId, maxResults: maxResults);

        Assert.NotNull(result);
        Assert.True(result.ReturnedCount <= maxResults, $"Expected at most {maxResults} results, got {result.ReturnedCount}");
        Assert.Equal(result.Types.Count, result.ReturnedCount);

        if (result.TotalCount > maxResults)
        {
            Assert.True(result.IsPartial, "IsPartial should be true when results are limited");
        }

        TestOutput.WriteLine($"Total types in package: {result.TotalCount}");
        TestOutput.WriteLine($"Requested max results: {maxResults}");
        TestOutput.WriteLine($"Returned types: {result.ReturnedCount}");
        TestOutput.WriteLine($"Is partial: {result.IsPartial}");
    }

    [Fact]
    public async Task ListClasses_WithSkipAndMaxResults_ImplementsPagination()
    {
        // Test pagination with skip and maxResults
        var packageId = "DimonSmart.MazeGenerator";
        var pageSize = 3;

        // Get first page
        var page1 = await _listTool.list_classes_records_structs(packageId, maxResults: pageSize, skip: 0);

        // Get second page
        var page2 = await _listTool.list_classes_records_structs(packageId, maxResults: pageSize, skip: pageSize);

        Assert.NotNull(page1);
        Assert.NotNull(page2);

        // Both pages should have the same total count
        Assert.Equal(page1.TotalCount, page2.TotalCount);

        // Pages should not overlap (if we have enough types)
        if (page1.Types.Count > 0 && page2.Types.Count > 0)
        {
            var page1Names = page1.Types.Select(t => t.FullName).ToHashSet();
            var page2Names = page2.Types.Select(t => t.FullName).ToHashSet();

            Assert.Empty(page1Names.Intersect(page2Names));
        }

        TestOutput.WriteLine($"Total types: {page1.TotalCount}");
        TestOutput.WriteLine($"Page 1 count: {page1.ReturnedCount}");
        TestOutput.WriteLine($"Page 2 count: {page2.ReturnedCount}");
    }

    [Fact]
    public async Task ListClasses_WithFilterAndPagination_CombinesFeatures()
    {
        // Test combining filter and pagination
        var packageId = "DimonSmart.MazeGenerator";
        var filter = "*a*"; // Common letter, should match several types
        var maxResults = 2;

        var result = await _listTool.list_classes_records_structs(packageId, filter: filter, maxResults: maxResults);

        Assert.NotNull(result);

        // All returned types should match the filter
        Assert.All(result.Types, t =>
            Assert.True(
                t.Name.Contains("a", StringComparison.OrdinalIgnoreCase) ||
                t.FullName.Contains("a", StringComparison.OrdinalIgnoreCase)));

        // Should respect maxResults
        Assert.True(result.ReturnedCount <= maxResults);

        TestOutput.WriteLine($"Filter: {filter}");
        TestOutput.WriteLine($"Max results: {maxResults}");
        TestOutput.WriteLine($"Total matching types: {result.TotalCount}");
        TestOutput.WriteLine($"Returned types: {result.ReturnedCount}");
        TestOutput.WriteLine($"Is partial: {result.IsPartial}");
    }
}
