using Microsoft.Extensions.Logging;

using Moq;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;
using NugetMcpServer.Tests.Helpers;

using Xunit;
using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Tools;

public class GetEnumDefinitionToolTests : TestBase
{
    private readonly Mock<ILogger<GetEnumDefinitionTool>> _loggerMock = new();
    private readonly NuGetPackageService _packageService;
    private readonly Mock<EnumFormattingService> _formattingServiceMock = new();

    public GetEnumDefinitionToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageService = CreateNuGetPackageService();
    }

    [Fact]
    public async Task GetEnumDefinition_Should_ThrowArgumentNullException_When_PackageIdIsEmpty()
    {
        // Arrange
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, _packageService, _formattingServiceMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => tool.GetEnumDefinition("", "SomeEnum"));
    }

    [Fact]
    public async Task GetEnumDefinition_Should_ThrowArgumentNullException_When_EnumNameIsEmpty()
    {
        // Arrange
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, _packageService, _formattingServiceMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => tool.GetEnumDefinition("SomePackage", ""));
    }

    // Integration tests for enum lookup with real packages
    [Fact]
    public async Task GetEnumDefinition_WithShortName_ReturnsDefinition()
    {
        var packageId = "System.ComponentModel.Annotations";
        var dataTypeEnumName = "DataType";

        var packageService = CreateNuGetPackageService();
        var formattingService = new EnumFormattingService();
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, packageService, formattingService);

        var definition = await tool.GetEnumDefinition(packageId, dataTypeEnumName);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("enum", definition);
        Assert.Contains("DataType", definition);
        Assert.DoesNotContain("not found in package", definition);
    }

    [Fact]
    public async Task GetEnumDefinition_WithFullName_ReturnsDefinition()
    {
        var packageId = "System.ComponentModel.Annotations";
        var fullDataTypeEnumName = "System.ComponentModel.DataAnnotations.DataType";

        var packageService = CreateNuGetPackageService();
        var formattingService = new EnumFormattingService();
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, packageService, formattingService);

        var definition = await tool.GetEnumDefinition(packageId, fullDataTypeEnumName);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("enum", definition);
        Assert.Contains("DataType", definition);
        Assert.DoesNotContain("not found in package", definition);
    }
}
