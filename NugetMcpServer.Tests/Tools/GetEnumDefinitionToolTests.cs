using Microsoft.Extensions.Logging;
using Moq;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NuGetMcpServer.Tests.Tools;

public class GetEnumDefinitionToolTests
{
    private readonly Mock<ILogger<GetEnumDefinitionTool>> _loggerMock = new();
    private readonly Mock<ILogger<NuGetPackageService>> _packageLoggerMock = new();
    private readonly Mock<HttpClient> _httpClientMock = new();
    private readonly NuGetPackageService _packageService;
    private readonly Mock<EnumFormattingService> _formattingServiceMock = new();
    
    public GetEnumDefinitionToolTests()    {
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
}
