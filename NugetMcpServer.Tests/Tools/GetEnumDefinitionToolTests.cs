using Microsoft.Extensions.Logging;

using Moq;

using NuGetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;

using Xunit;
using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools;

public class GetEnumDefinitionToolTests : TestBase
{
    private readonly Mock<ILogger<GetEnumDefinitionTool>> _loggerMock = new();
    private readonly Mock<ILogger<ArchiveProcessingService>> _archiveLoggerMock = new();
    private readonly NuGetPackageService _packageService;
    private readonly Mock<EnumFormattingService> _formattingServiceMock = new();

    public GetEnumDefinitionToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageService = CreateNuGetPackageService();
    }

    [Theory]
    [InlineData("", "SomeEnum")]
    [InlineData("SomePackage", "")]
    public async Task GetEnumDefinition_InvalidArguments_ThrowsArgumentNullException(string packageId, string enumName)
    {
        var archiveService = new ArchiveProcessingService(_archiveLoggerMock.Object, _packageService);
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, _packageService, _formattingServiceMock.Object, archiveService);

        await Assert.ThrowsAsync<ArgumentNullException>(() => tool.get_enum_definition(packageId, enumName));
    }

    // Integration tests for enum lookup with real packages
    [Fact]
    public async Task GetEnumDefinition_WithShortName_ReturnsDefinition()
    {
        var packageId = "System.ComponentModel.Annotations";
        var dataTypeEnumName = "DataType";

        var packageService = CreateNuGetPackageService();
        var formattingService = new EnumFormattingService();
        var archiveService = new ArchiveProcessingService(_archiveLoggerMock.Object, packageService);
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, packageService, formattingService, archiveService);

        var definition = await tool.get_enum_definition(packageId, dataTypeEnumName);

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
        var archiveService = new ArchiveProcessingService(_archiveLoggerMock.Object, packageService);
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, packageService, formattingService, archiveService);

        var definition = await tool.get_enum_definition(packageId, fullDataTypeEnumName);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("enum", definition);
        Assert.Contains("DataType", definition);
        Assert.DoesNotContain("not found in package", definition);
    }
}
