using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

public class PackageSearchResult
{
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public IReadOnlyCollection<PackageInfo> Packages { get; set; } = [];
    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/* NUGET PACKAGE SEARCH RESULTS FOR: {Query} */");

        sb.AppendLine($"/* FOUND {TotalCount} PACKAGES (SHOWING TOP {Packages.Count}) */");
        sb.AppendLine();

        foreach (var package in Packages.OrderByDescending(p => p.DownloadCount))
        {
            sb.AppendLine($"## {package.Id} v{package.Version}");
            sb.AppendLine($"**Downloads**: {package.DownloadCount:N0}");

            if (!string.IsNullOrEmpty(package.Description))
                sb.AppendLine($"**Description**: {package.Description}");

            if (package.FoundByKeywords.Any())
            {
                sb.AppendLine($"**Found by keywords**: {string.Join(", ", package.FoundByKeywords)}");
            }

            if (!string.IsNullOrEmpty(package.ProjectUrl))
            {
                sb.AppendLine($"**Project URL**: {package.ProjectUrl}");
            }

            if (package.Tags?.Any() == true)
            {
                sb.AppendLine($"**Tags**: {string.Join(", ", package.Tags)}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
