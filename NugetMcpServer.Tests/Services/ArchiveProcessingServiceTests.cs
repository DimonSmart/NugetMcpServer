using NuGetMcpServer.Services;
using Xunit;

namespace NuGetMcpServer.Tests.Services;

public sealed class ArchiveProcessingServiceTests
{
    [Fact]
    public void GetCurrentFramework_ParsesAppContextTargetFrameworkName()
    {
        var current = ArchiveProcessingService.GetCurrentFramework();

        Assert.False(current.IsUnsupported);
        Assert.Contains("net", current.GetShortFolderName(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(new[] { "netstandard2.0", "net8.0" }, "net8.0")]
    [InlineData(new[] { "netstandard2.0" }, "netstandard2.0")]
    [InlineData(new[] { "net472" }, "net472")]
    public void SelectNearestFramework_ChoosesCompatibleFramework(string[] frameworks, string expected)
    {
        var selected = ArchiveProcessingService.SelectNearestFramework(frameworks);

        Assert.Equal(expected, selected);
    }

    [Fact]
    public void SelectNearestAssemblyFiles_SelectsFrameworkPerAssemblyName()
    {
        var selected = ArchiveProcessingService.SelectNearestAssemblyFiles(
        [
            ("lib/netstandard2.0/A.dll", "netstandard2.0"),
            ("lib/netstandard2.0/B.dll", "netstandard2.0"),
            ("lib/net8.0/A.dll", "net8.0")
        ]);

        Assert.Contains(selected, file => file.PackagePath == "lib/net8.0/A.dll");
        Assert.Contains(selected, file => file.PackagePath == "lib/netstandard2.0/B.dll");
        Assert.DoesNotContain(selected, file => file.PackagePath == "lib/netstandard2.0/A.dll");
    }
}
