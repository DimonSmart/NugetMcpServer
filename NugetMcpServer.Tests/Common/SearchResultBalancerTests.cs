using System.Collections.Generic;
using System.Linq;
using NuGetMcpServer.Common;
using NuGetMcpServer.Services;
using Xunit;

namespace NugetMcpServer.Tests.Common;

public class SearchResultBalancerTests
{
    private static PackageInfo P(string prefix, int index) => new PackageInfo { Id = $"{prefix}{index}", Version = "1.0" };

    [Fact]
    public void Balance_PrefersSmallerSets()
    {
        var set1 = new SearchResultSet("a", new List<PackageInfo>{P("A",1),P("A",2),P("A",3),P("A",4),P("A",5),P("A",6),P("A",7),P("A",8),P("A",9),P("A",10)});
        var set2 = new SearchResultSet("b", new List<PackageInfo>());
        var set3 = new SearchResultSet("c", new List<PackageInfo>());
        for(int i=1;i<=100;i++) set3.Packages.Add(P("C",i));

        var result = SearchResultBalancer.Balance(new[]{set1,set2,set3},10);

        Assert.Equal(10, result.Count);
        Assert.Equal(5, result.Count(p => p.Id.StartsWith("A")));
        Assert.Equal(5, result.Count(p => p.Id.StartsWith("C")));
    }

    [Fact]
    public void Balance_SingleSmallSetIncluded()
    {
        var small = new SearchResultSet("s", new List<PackageInfo>{P("S",1)});
        var large = new SearchResultSet("l", new List<PackageInfo>());
        for(int i=1;i<=20;i++) large.Packages.Add(P("L",i));

        var result = SearchResultBalancer.Balance(new[]{small,large},10);

        Assert.Equal(10, result.Count);
        Assert.Contains(result, p => p.Id=="S1");
    }
}
