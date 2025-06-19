using System.Collections.Generic;
using System.Linq;

namespace NuGetMcpServer.Common;

public record SearchResultSet(string Keyword, List<Services.PackageInfo> Packages);

public static class SearchResultBalancer
{
    public static List<Services.PackageInfo> Balance(IEnumerable<SearchResultSet> sets, int maxResults)
    {
        var setList = sets.Where(s => s.Packages.Count > 0).ToList();
        if (!setList.Any() || maxResults <= 0)
            return [];

        var sorted = setList.OrderBy(s => s.Packages.Count).ToList();
        var indexes = sorted.ToDictionary(s => s, _ => 0);
        var result = new List<Services.PackageInfo>();
        var usedIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        while (result.Count < maxResults && sorted.Any(s => indexes[s] < s.Packages.Count))
        {
            foreach (var set in sorted)
            {
                var idx = indexes[set];
                if (idx >= set.Packages.Count)
                    continue;

                indexes[set] = idx + 1;
                var pkg = set.Packages[idx];
                if (usedIds.Add(pkg.Id))
                {
                    result.Add(pkg);
                    if (result.Count >= maxResults)
                        break;
                }
            }
        }

        return result;
    }
}
