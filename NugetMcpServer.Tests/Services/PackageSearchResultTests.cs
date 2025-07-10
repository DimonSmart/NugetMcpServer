using NuGetMcpServer.Services;
using NuGetMcpServer.Services.Formatters;

namespace NuGetMcpServer.Tests.Services;

public class PackageSearchResultTests
{
    [Fact]
    public void Format_IncludesFoundByKeywords()
    {
        var package1 = new PackageInfo
        {
            PackageId = "TestPackage1",
            Version = "1.0.0",
            Description = "Test package description",
            DownloadCount = 1000,
            FoundByKeywords = ["test", "sample"]
        };

        var package2 = new PackageInfo
        {
            PackageId = "TestPackage2",
            Version = "2.0.0",
            DownloadCount = 500,
            FoundByKeywords = ["example"]
        }; var result = new PackageSearchResult
        {
            Query = "test query",
            TotalCount = 2,
            Packages = [package1, package2]
        };

        var formatted = result.Format();

        Assert.Contains("NuGet PACKAGE SEARCH RESULTS FOR: test query", formatted);
        Assert.Contains("TestPackage1 v1.0.0", formatted);
        Assert.Contains("**Found by keywords**: test, sample", formatted);
        Assert.Contains("TestPackage2 v2.0.0", formatted);
        Assert.Contains("**Found by keywords**: example", formatted);
        Assert.DoesNotContain("AI-GENERATED PACKAGE NAMES", formatted);
    }

    [Fact]
    public void Format_OrdersByDownloadCount()
    {
        var package1 = new PackageInfo
        {
            PackageId = "LessPopular",
            Version = "1.0.0",
            DownloadCount = 100,
            FoundByKeywords = ["test"]
        };

        var package2 = new PackageInfo
        {
            PackageId = "MorePopular",
            Version = "1.0.0",
            DownloadCount = 1000,
            FoundByKeywords = ["test"]
        }; var result = new PackageSearchResult
        {
            Query = "test",
            TotalCount = 2,
            Packages = [package1, package2] // Less popular first
        };

        var formatted = result.Format();
        var morePopularIndex = formatted.IndexOf("MorePopular");
        var lessPopularIndex = formatted.IndexOf("LessPopular");

        Assert.True(morePopularIndex < lessPopularIndex, "More popular package should appear first");
    }
}
