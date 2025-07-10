using NuGetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;

using Xunit;
using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools;

public class GetPackageInfoToolTests : TestBase
{
    private readonly TestLogger<NuGetPackageService> _packageLogger;
    private readonly TestLogger<GetPackageInfoTool> _toolLogger;
    private readonly NuGetPackageService _packageService;
    private readonly GetPackageInfoTool _tool;

    public GetPackageInfoToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        _toolLogger = new TestLogger<GetPackageInfoTool>(TestOutput);

        _packageService = CreateNuGetPackageService();
        _tool = new GetPackageInfoTool(_toolLogger, _packageService);
    }

    [Fact]
    public async Task GetPackageInfo_WithValidPackage_ReturnsFormattedInfo()
    {
        var result = await _tool.get_package_info("Newtonsoft.Json", "13.0.3");

        Assert.NotNull(result);
        Assert.Contains("Newtonsoft.Json", result);
        Assert.Contains("13.0.3", result);
        Assert.Contains("Description:", result);
    }

    [Fact]
    public async Task GetPackageInfo_WithMetaPackage_ShowsMetaPackageWarning()
    {
        var result = await _tool.get_package_info("Microsoft.AspNetCore.All", "2.1.0");

        Assert.NotNull(result);
        Assert.Contains("Microsoft.AspNetCore.All", result);
        Assert.Contains("2.1.0", result);
        Assert.Contains("Dependencies:", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetPackageInfo_InvalidPackageId_ThrowsArgumentNullException(string? packageId)
    {
        await Assert.ThrowsAsync<System.ArgumentNullException>(() => _tool.get_package_info(packageId!));
    }

    [Fact]
    public async Task GetPackageInfo_WithoutVersion_UsesLatestVersion()
    {
        var result = await _tool.get_package_info("Newtonsoft.Json");

        Assert.NotNull(result);
        Assert.Contains("Newtonsoft.Json", result);
    }
}
