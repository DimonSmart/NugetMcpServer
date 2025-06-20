using NuGetMcpServer.Common;
using NuGetMcpServer.Services;

namespace NugetMcpServer.Tests.Common;

public class SearchResultBalancerTests
{
    private static PackageInfo P(string prefix, int index) => new() { Id = $"{prefix}{index}", Version = "1.0" }; [Fact]
    public void Balance_PrefersSmallerSets()
    {
        SearchResultSet set1 = new("a", [P("A", 1), P("A", 2), P("A", 3), P("A", 4), P("A", 5), P("A", 6), P("A", 7), P("A", 8), P("A", 9), P("A", 10)]);
        SearchResultSet set2 = new("b", []);
        List<PackageInfo> set3Packages = new();
        for (int i = 1; i <= 100; i++)
        {
            set3Packages.Add(P("C", i));
        }

        SearchResultSet set3 = new("c", set3Packages);

        var result = SearchResultBalancer.Balance([set1, set2, set3], 10);

        Assert.Equal(10, result.Count);
        Assert.Equal(5, result.Count(p => p.Id.StartsWith("A")));
        Assert.Equal(5, result.Count(p => p.Id.StartsWith("C")));
    }

    [Fact]
    public void Balance_SingleSmallSetIncluded()
    {
        SearchResultSet small = new("s", [P("S", 1)]);
        List<PackageInfo> largePackages = new();
        for (int i = 1; i <= 20; i++)
        {
            largePackages.Add(P("L", i));
        }

        SearchResultSet large = new("l", largePackages);

        var result = SearchResultBalancer.Balance([small, large], 10);

        Assert.Equal(10, result.Count);
        Assert.Contains(result, p => p.Id == "S1");
    }

    [Fact]
    public void Balance_RemovesDuplicates()
    {
        SearchResultSet set1 = new("a", [P("X", 1)]);
        SearchResultSet set2 = new("b", [P("X", 1), P("Y", 1)]);

        var result = SearchResultBalancer.Balance([set1, set2], 5);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Id == "X1");
        Assert.Contains(result, p => p.Id == "Y1");
    }

    [Fact]
    public void Balance_TracksFoundByKeywords()
    {
        SearchResultSet set1 = new("keyword1", [P("X", 1), P("Y", 1)]);
        SearchResultSet set2 = new("keyword2", [P("X", 1), P("Z", 1)]);

        var result = SearchResultBalancer.Balance([set1, set2], 5);

        Assert.Equal(3, result.Count);

        var packageX = result.First(p => p.Id == "X1");
        Assert.Contains("keyword1", packageX.FoundByKeywords);
        Assert.Contains("keyword2", packageX.FoundByKeywords);
        Assert.Equal(2, packageX.FoundByKeywords.Count);

        var packageY = result.First(p => p.Id == "Y1");
        Assert.Contains("keyword1", packageY.FoundByKeywords);
        Assert.Single(packageY.FoundByKeywords);

        var packageZ = result.First(p => p.Id == "Z1");
        Assert.Contains("keyword2", packageZ.FoundByKeywords);
        Assert.Single(packageZ.FoundByKeywords);
    }

    [Fact]
    public void Balance_TracksAllKeywords_EvenWhenLimitReached()
    {
        SearchResultSet set1 = new("json", [P("PackageA", 1), P("PackageB", 1)]);
        SearchResultSet set2 = new("serialize", [P("PackageA", 1), P("PackageC", 1)]);
        SearchResultSet set3 = new("convert", [P("PackageA", 1), P("PackageD", 1)]);

        // Limit to 2 packages, but PackageA should have all 3 keywords
        var result = SearchResultBalancer.Balance([set1, set2, set3], 2);

        Assert.Equal(2, result.Count);

        var packageA = result.First(p => p.Id == "PackageA1");
        Assert.Contains("json", packageA.FoundByKeywords);
        Assert.Contains("serialize", packageA.FoundByKeywords);
        Assert.Contains("convert", packageA.FoundByKeywords);
        Assert.Equal(3, packageA.FoundByKeywords.Count);
    }
}
