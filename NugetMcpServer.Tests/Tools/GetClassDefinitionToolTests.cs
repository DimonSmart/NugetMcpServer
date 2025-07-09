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

        _packageService = CreateNuGetPackageService();
        _formattingService = new ClassFormattingService();
        _defTool = new GetClassDefinitionTool(_defToolLogger, _packageService, _formattingService);
    }

    [Fact]
    public async Task GetClassDefinition_WithSpecificClass_ReturnsDefinition()
    {
        var packageId = "DimonSmart.MazeGenerator";
        var pointClassName = "Point";
        var version = await _packageService.GetLatestVersion(packageId);

        var definition = await _defTool.GetClassDefinition(packageId, pointClassName, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("class", definition);
        Assert.Contains("Point", definition);

        TestOutput.WriteLine("\n========== TEST OUTPUT: Point CLASS DEFINITION ==========");
        TestOutput.WriteLine(definition);
        TestOutput.WriteLine("======================================================\n");
    }

    [Fact]
    public async Task GetClassDefinition_WithGenericClass_ReturnsFormattedDefinition()
    {
        var packageId = "DimonSmart.MazeGenerator";
        var genericMazeClassName = "Maze";
        var version = await _packageService.GetLatestVersion(packageId);

        var definition = await _defTool.GetClassDefinition(packageId, genericMazeClassName, version);

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
    {
        var packageId = "DimonSmart.MazeGenerator";
        var fullPointClassName = "DimonSmart.MazeGenerator.Point";
        var version = await _packageService.GetLatestVersion(packageId);

        var definition = await _defTool.GetClassDefinition(packageId, fullPointClassName, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("class", definition);
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

        var definition = await _defTool.GetClassDefinition(packageId, className, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("not found in package", definition);

        TestOutput.WriteLine("\n========== TEST OUTPUT: NON-EXISTENT CLASS ==========");
        TestOutput.WriteLine(definition);
        TestOutput.WriteLine("==================================================\n");
    }
}
