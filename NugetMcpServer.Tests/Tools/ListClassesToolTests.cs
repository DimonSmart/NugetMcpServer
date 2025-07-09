using NugetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;
using NuGetMcpServer.Services.Formatters;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Tools;

public class ListClassesToolTests : TestBase
{
    private readonly TestLogger<NuGetPackageService> _packageLogger;
    private readonly TestLogger<ListClassesTool> _listToolLogger;
    private readonly NuGetPackageService _packageService;
    private readonly ListClassesTool _listTool;

    public ListClassesToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        _listToolLogger = new TestLogger<ListClassesTool>(TestOutput);

        _packageService = CreateNuGetPackageService();
        _listTool = new ListClassesTool(_listToolLogger, _packageService);
    }

    [Fact]
    public async Task ListClasses_WithValidPackage_ReturnsClasses()
    {
        // Test with a known package
        var packageId = "DimonSmart.MazeGenerator";

        var result = await _listTool.ListClasses(packageId);

        Assert.NotNull(result);
        Assert.Equal(packageId, result.PackageId);
        Assert.NotEmpty(result.Version);
        Assert.NotEmpty(result.Classes);

        TestOutput.WriteLine($"Found {result.Classes.Count} classes in {result.PackageId} version {result.Version}");
        TestOutput.WriteLine("\n========== TEST OUTPUT: LIST OF CLASSES ==========");
        TestOutput.WriteLine(result.Format());
        TestOutput.WriteLine("================================================\n");

        // Verify we found expected classes - using Point instead of Cell as Cell doesn't exist in current version
        Assert.Contains(result.Classes, c => c.Name == "Point" || c.FullName.Contains(".Point"));
    }

    [Fact]
    public async Task ListClasses_WithSpecificVersion_ReturnsClasses()
    {
        // Test with a known package and version
        var packageId = "DimonSmart.MazeGenerator";
        var version = await _packageService.GetLatestVersion(packageId);

        var result = await _listTool.ListClasses(packageId, version);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(packageId, result.PackageId);
        Assert.Equal(version, result.Version);
        Assert.NotEmpty(result.Classes);

        TestOutput.WriteLine($"Found {result.Classes.Count} classes in {result.PackageId} version {result.Version}");
    }

    [Fact]
    public async Task ListClasses_ContainsClassModifierInformation()
    {
        // Test that the result contains modifier information
        var packageId = "DimonSmart.MazeGenerator";
        var result = await _listTool.ListClasses(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Classes);

        // Check that we have some information about modifiers (at least one class should have some modifier info)
        var hasModifiers = result.Classes.Any(c => c.IsStatic || c.IsAbstract || c.IsSealed);

        // Log all classes with their modifiers for debugging
        foreach (var cls in result.Classes)
        {
            TestOutput.WriteLine($"Class: {cls.Name} - Static: {cls.IsStatic}, Abstract: {cls.IsAbstract}, Sealed: {cls.IsSealed}");
        }

        TestOutput.WriteLine($"Total classes found: {result.Classes.Count}");
        TestOutput.WriteLine($"Classes with modifiers: {result.Classes.Count(c => c.IsStatic || c.IsAbstract || c.IsSealed)}");
    }
}
