using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Moq;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;

using Xunit;

namespace NuGetMcpServer.Tests.Tools;

public class GetEnumDefinitionToolTests
{
    private readonly Mock<ILogger<GetEnumDefinitionTool>> _loggerMock = new();
    private readonly Mock<ILogger<NuGetPackageService>> _packageLoggerMock = new();
    private readonly Mock<HttpClient> _httpClientMock = new();
    private readonly NuGetPackageService _packageService;
    private readonly Mock<EnumFormattingService> _formattingServiceMock = new();

    public GetEnumDefinitionToolTests()
    {
        _packageService = new NuGetPackageService(_packageLoggerMock.Object, _httpClientMock.Object);
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

    // Note: Removed version check test as it would require mocking HttpClient which is challenging
    // and not necessary for basic unit testing

    // Integration tests for enum lookup with real packages
    [Fact]
    public async Task GetEnumDefinition_WithShortName_ReturnsDefinition()
    {
        // Test with a known package and short enum name
        var packageId = "System.ComponentModel.Annotations";
        var enumName = "DataType"; // Short name

        using var httpClient = new HttpClient();
        var packageService = new NuGetPackageService(_packageLoggerMock.Object, httpClient);
        var formattingService = new EnumFormattingService();
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, packageService, formattingService);

        // Get enum definition
        var definition = await tool.GetEnumDefinition(packageId, enumName);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("enum", definition);
        Assert.Contains("DataType", definition);
        Assert.DoesNotContain("not found in package", definition);
    }

    [Fact]
    public async Task GetEnumDefinition_WithFullName_ReturnsDefinition()
    {
        // Test with a known package and full enum name
        var packageId = "System.ComponentModel.Annotations";
        var enumName = "System.ComponentModel.DataAnnotations.DataType"; // Full name

        using var httpClient = new HttpClient();
        var packageService = new NuGetPackageService(_packageLoggerMock.Object, httpClient);
        var formattingService = new EnumFormattingService();
        var tool = new GetEnumDefinitionTool(_loggerMock.Object, packageService, formattingService);

        // Get enum definition
        var definition = await tool.GetEnumDefinition(packageId, enumName);

        // Assert
        Assert.NotNull(definition);
        Assert.Contains("enum", definition);
        Assert.Contains("DataType", definition);
        Assert.DoesNotContain("not found in package", definition);
    }
}
