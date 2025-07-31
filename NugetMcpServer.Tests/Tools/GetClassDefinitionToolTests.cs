using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools;

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
        var archiveService = new ArchiveProcessingService(new TestLogger<ArchiveProcessingService>(TestOutput), _packageService);
        _defTool = new GetClassDefinitionTool(_defToolLogger, _packageService, _formattingService, archiveService);
    }

    [Fact]
    public async Task GetClassDefinition_WithSpecificClass_ReturnsDefinition()
    {
        var packageId = "DimonSmart.MazeGenerator";
        var pointClassName = "Point";
        var version = await _packageService.GetLatestVersion(packageId);

        var definition = await _defTool.get_class_or_record_definition(packageId, pointClassName, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("record", definition);
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

        var definition = await _defTool.get_class_or_record_definition(packageId, genericMazeClassName, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("class", definition);
        Assert.DoesNotContain("not found in package", definition);

        TestOutput.WriteLine("\n========== TEST OUTPUT: Maze CLASS DEFINITION ==========");
        TestOutput.WriteLine(definition);
        TestOutput.WriteLine("======================================================\n");
    }


    [Fact]
    public async Task GetClassDefinition_WithFullName_ReturnsDefinition()
    {
        var packageId = "DimonSmart.MazeGenerator";
        var fullPointClassName = "DimonSmart.MazeGenerator.Point";
        var version = await _packageService.GetLatestVersion(packageId);

        var definition = await _defTool.get_class_or_record_definition(packageId, fullPointClassName, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("record", definition);
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

        var definition = await _defTool.get_class_or_record_definition(packageId, className, version);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("not found in package", definition);

        TestOutput.WriteLine("\n========== TEST OUTPUT: NON-EXISTENT CLASS ==========");
        TestOutput.WriteLine(definition);
        TestOutput.WriteLine("==================================================\n");
    }
}
