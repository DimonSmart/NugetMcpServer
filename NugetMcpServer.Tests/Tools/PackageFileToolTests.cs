using System;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools;

public class PackageFileToolTests : TestBase
{
    private readonly TestLogger<NuGetPackageService> _packageLogger;
    private readonly TestLogger<PackageFileTool> _toolLogger;
    private readonly NuGetPackageService _packageService;
    private readonly PackageFileTool _tool;

    public PackageFileToolTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        _toolLogger = new TestLogger<PackageFileTool>(TestOutput);
        _packageService = CreateNuGetPackageService();
        _tool = new PackageFileTool(_toolLogger, _packageService);
    }

    [Fact]
    public async Task ListPackageFiles_ReturnsFiles()
    {
        var result = await _tool.list_package_files("Newtonsoft.Json", "13.0.3");
        Assert.NotEmpty(result.Files);
        Assert.Contains(result.Files, f => f.EndsWith("LICENSE.md"));
    }

    [Fact]
    public async Task GetPackageFile_TextFile_ReturnsContent()
    {
        var result = await _tool.get_package_file("Newtonsoft.Json", "LICENSE.md", "13.0.3", bytes: 100);
        Assert.False(result.IsBinary);
        Assert.Contains("MIT", result.Content);
    }

    [Fact]
    public async Task GetPackageFile_BinaryFile_ReturnsBase64()
    {
        var result = await _tool.get_package_file("Newtonsoft.Json", "lib/net45/Newtonsoft.Json.dll", "13.0.3", bytes: 10);
        Assert.True(result.IsBinary);
        var data = Convert.FromBase64String(result.Content);
        Assert.Equal(10, data.Length);
    }
}

