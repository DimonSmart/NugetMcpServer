using NugetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Tools;

public class GetClassDefinitionToolTests : TestBase
{
    private readonly TestLogger<NuGetPackageService> _packageLogger;
    private readonly TestLogger<GetClassDefinitionTool> _defToolLogger;
    private readonly NuGetPackageService _packageService;
    private readonly ClassFormattingService _formattingService;
    private readonly GetClassDefinitionTool _defTool;

    public GetClassDefinitionToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        _defToolLogger = new TestLogger<GetClassDefinitionTool>(TestOutput);

        _packageService = new NuGetPackageService(_packageLogger, HttpClient);
        _formattingService = new ClassFormattingService();
        _defTool = new GetClassDefinitionTool(_defToolLogger, _packageService, _formattingService);
    }

    [Fact]
    public async Task GetClassDefinition_WithSpecificClass_ReturnsDefinition()
    {        // Test with a known package and class
        var packageId = "DimonSmart.MazeGenerator";
        var className = "Point"; // Using Point instead of Cell as Cell doesn't exist in current version
        var version = await _packageService.GetLatestVersion(packageId);

        // Get class definition
        var definition = await _defTool.GetClassDefinition(packageId, className, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("class", definition); // or "struct" if Point is a struct
        Assert.Contains("Point", definition); TestOutput.WriteLine("\n========== TEST OUTPUT: Point CLASS DEFINITION ==========");
        TestOutput.WriteLine(definition);
        TestOutput.WriteLine("======================================================\n");
    }

    [Fact]
    public async Task GetClassDefinition_WithGenericClass_ReturnsFormattedDefinition()
    {
        // Test with a known generic class
        var packageId = "DimonSmart.MazeGenerator";
        var className = "Maze";  // Generic class (actually Maze<T>)
        var version = await _packageService.GetLatestVersion(packageId);

        // Get class definition
        var definition = await _defTool.GetClassDefinition(packageId, className, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("class", definition);
        Assert.DoesNotContain("not found in package", definition);

        TestOutput.WriteLine("\n========== TEST OUTPUT: Maze CLASS DEFINITION ==========");
        TestOutput.WriteLine(definition);
        TestOutput.WriteLine("======================================================\n");
    }

    [Fact]
    public async Task GetClassDefinition_WithPackageAndListTool_WorksWithBothTools()
    {
        // This test verifies that both tools can work together on the same package
        var listToolLogger = new TestLogger<ListClassesTool>(TestOutput);
        var listTool = new ListClassesTool(listToolLogger, _packageService);

        var packageId = "DimonSmart.MazeGenerator";

        // Step 1: List classes in the package
        var result = await listTool.ListClasses(packageId);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Classes);

        // Step 2: Get definition of one of the classes
        var mazeClass = result.Classes.FirstOrDefault(c => c.Name.StartsWith("Cell"));
        if (mazeClass != null)
        {
            var definition = await _defTool.GetClassDefinition(
                packageId,
                mazeClass.Name,
                result.Version);

            // Assert
            Assert.Contains("class", definition);
            Assert.Contains("Cell", definition);

            TestOutput.WriteLine("\n========== TEST OUTPUT: RESULT OF GetClassDefinition ==========");
            TestOutput.WriteLine(definition);
            TestOutput.WriteLine("=============================================================\n");
        }
    }

    [Fact]
    public async Task GetClassDefinition_WithFullName_ReturnsDefinition()
    {        // Test with a known package and full class name
        var packageId = "DimonSmart.MazeGenerator";
        var className = "DimonSmart.MazeGenerator.Point"; // Full name with namespace, using Point instead of Cell
        var version = await _packageService.GetLatestVersion(packageId);

        // Get class definition
        var definition = await _defTool.GetClassDefinition(packageId, className, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("class", definition); // or "struct" if Point is a struct
        Assert.Contains("Point", definition);
        Assert.DoesNotContain("not found in package", definition);

        TestOutput.WriteLine("\n========== TEST OUTPUT: Point CLASS DEFINITION (FULL NAME) ==========");
        TestOutput.WriteLine(definition);
        TestOutput.WriteLine("===================================================================\n");
    }

    [Fact]
    public async Task GetClassDefinition_WithNonExistentClass_ReturnsNotFound()
    {
        // Test with a non-existent class
        var packageId = "DimonSmart.MazeGenerator";
        var className = "NonExistentClass";
        var version = await _packageService.GetLatestVersion(packageId);

        // Get class definition
        var definition = await _defTool.GetClassDefinition(packageId, className, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("not found in package", definition);

        TestOutput.WriteLine("\n========== TEST OUTPUT: NON-EXISTENT CLASS ==========");
        TestOutput.WriteLine(definition);
        TestOutput.WriteLine("==================================================\n");
    }
}
