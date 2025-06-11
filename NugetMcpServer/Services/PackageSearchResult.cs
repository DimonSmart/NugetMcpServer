using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

/// <summary>
/// Response model for package search results
/// </summary>
public class PackageSearchResult
{
    /// <summary>
    /// The original search query
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Total number of packages found
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// List of found packages
    /// </summary>
    public List<PackageInfo> Packages { get; set; } = [];

    /// <summary>
    /// Indicates if the search used AI-generated package names (fuzzy search)
    /// </summary>
    public bool UsedAiKeywords { get; set; }

    /// <summary>
    /// AI-generated package names used for fuzzy search (if any)
    /// </summary>
    public string? AiKeywords { get; set; }

    /// <summary>
    /// Returns a formatted string representation of the search results
    /// </summary>
    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/* NUGET PACKAGE SEARCH RESULTS FOR: {Query} */");
        if (UsedAiKeywords && !string.IsNullOrEmpty(AiKeywords))
        {
            sb.AppendLine($"/* AI-GENERATED PACKAGE NAMES: {AiKeywords} */");
        }

        sb.AppendLine($"/* FOUND {TotalCount} PACKAGES (SHOWING TOP {Packages.Count}) */");
        sb.AppendLine();

        foreach (var package in Packages.OrderByDescending(p => p.DownloadCount))
        {
            sb.AppendLine($"## {package.Id} v{package.Version}");
            sb.AppendLine($"**Downloads**: {package.DownloadCount:N0}");

            if (!string.IsNullOrEmpty(package.Description))
            {
                sb.AppendLine($"**Description**: {package.Description}");
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
