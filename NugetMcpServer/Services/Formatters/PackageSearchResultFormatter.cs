using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services.Formatters;

public static class PackageSearchResultFormatter
{
    public static string Format(this PackageSearchResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/* NUGET PACKAGE SEARCH RESULTS FOR: {result.Query} */");

        sb.AppendLine($"/* FOUND {result.TotalCount} PACKAGES (SHOWING TOP {result.Packages.Count}) */");
        sb.AppendLine();

        foreach (var package in result.Packages.OrderByDescending(p => p.DownloadCount))
        {
            sb.AppendLine($"## {package.PackageId} v{package.Version}");
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
