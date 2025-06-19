using NuGetMcpServer.Common;
using NuGetMcpServer.Services;

namespace NugetMcpServer.Tests.Common;

public class SearchResultBalancerTests
{
    private static PackageInfo P(string prefix, int index) => new() { Id = $"{prefix}{index}", Version = "1.0" }; [Fact]
    public void Balance_PrefersSmallerSets()
    {
        SearchResultSet set1 = new("a", [P("A", 1), P("A", 2), P("A", 3), P("A", 4), P("A", 5), P("A", 6), P("A", 7), P("A", 8), P("A", 9), P("A", 10)]);
        SearchResultSet set2 = new SearchResultSet("b", []);
        List<PackageInfo> set3Packages = new List<PackageInfo>();
        for (int i = 1; i <= 100; i++)
        {
            set3Packages.Add(P("C", i));
        }

        SearchResultSet set3 = new SearchResultSet("c", set3Packages);

        List<PackageInfo> result = SearchResultBalancer.Balance([set1, set2, set3], 10);

        Assert.Equal(10, result.Count);
        Assert.Equal(5, result.Count(p => p.Id.StartsWith("A")));
        Assert.Equal(5, result.Count(p => p.Id.StartsWith("C")));
    }

    [Fact]
    public void Balance_SingleSmallSetIncluded()
    {
        SearchResultSet small = new SearchResultSet("s", [P("S", 1)]);
        List<PackageInfo> largePackages = new List<PackageInfo>();
        for (int i = 1; i <= 20; i++)
        {
            largePackages.Add(P("L", i));
        }

        SearchResultSet large = new SearchResultSet("l", largePackages);

        List<PackageInfo> result = SearchResultBalancer.Balance([small, large], 10);

        Assert.Equal(10, result.Count);
        Assert.Contains(result, p => p.Id == "S1");
    }

    [Fact]
    public void Balance_RemovesDuplicates()
    {
        SearchResultSet set1 = new("a", [P("X", 1)]);
        SearchResultSet set2 = new("b", [P("X", 1), P("Y", 1)]);

        List<PackageInfo> result = SearchResultBalancer.Balance([set1, set2], 5);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Id == "X1");
        Assert.Contains(result, p => p.Id == "Y1");
    }
}
