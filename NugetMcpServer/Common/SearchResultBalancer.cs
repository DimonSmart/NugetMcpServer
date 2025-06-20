using System.Collections.Generic;
using System.Linq;

namespace NuGetMcpServer.Common;

public record SearchResultSet(string Keyword, IReadOnlyList<Services.PackageInfo> Packages);

public static class SearchResultBalancer
{
    public static IReadOnlyCollection<Services.PackageInfo> Balance(IEnumerable<SearchResultSet> sets, int maxResults)
    {
        var setList = sets.Where(s => s.Packages.Count > 0).ToList();
        if (!setList.Any() || maxResults <= 0)
            return [];

        // First pass: collect all keywords for all packages
        var allPackageKeywords = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);
        var packageInstances = new Dictionary<string, Services.PackageInfo>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var set in setList)
        {
            foreach (var pkg in set.Packages)
            {
                if (!allPackageKeywords.ContainsKey(pkg.Id))
                {
                    allPackageKeywords[pkg.Id] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    packageInstances[pkg.Id] = pkg; // Store first instance for data copying
                }
                allPackageKeywords[pkg.Id].Add(set.Keyword);
            }
        }

        // Second pass: balanced selection with complete keyword information
        var sorted = setList.OrderBy(s => s.Packages.Count).ToList();
        var indexes = sorted.ToDictionary(s => s, _ => 0);
        var result = new List<Services.PackageInfo>();
        var selectedPackageIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        while (result.Count < maxResults && sorted.Any(s => indexes[s] < s.Packages.Count))
        {
            foreach (var set in sorted)
            {
                var idx = indexes[set];
                if (idx >= set.Packages.Count)
                    continue;

                indexes[set] = idx + 1;
                var pkg = set.Packages[idx];

                if (selectedPackageIds.Add(pkg.Id))
                {
                    var pkgCopy = new Services.PackageInfo
                    {
                        Id = pkg.Id,
                        Version = pkg.Version,
                        Description = pkg.Description,
                        DownloadCount = pkg.DownloadCount,
                        ProjectUrl = pkg.ProjectUrl,
                        Tags = pkg.Tags,
                        Authors = pkg.Authors,
                        FoundByKeywords = allPackageKeywords[pkg.Id].ToList()
                    };
                    result.Add(pkgCopy);

                    if (result.Count >= maxResults)
                        break;
                }
            }
        }

        return result;
    }
}
