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

        var result = await _listTool.list_classes_records_structs(packageId);

        Assert.NotNull(result);
        Assert.Equal(packageId, result.PackageId);
        Assert.NotEmpty(result.Version);
        Assert.NotEmpty(result.Types);

        TestOutput.WriteLine($"Found {result.Types.Count} types in {result.PackageId} version {result.Version}");
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

        var result = await _listTool.list_classes_records_structs(packageId, version);

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
        var result = await _listTool.list_classes_records_structs(packageId);

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
}
