using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;

using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class SearchPackagesTool(ILogger<SearchPackagesTool> logger, PackageSearchService searchService) : McpToolBase<SearchPackagesTool>(logger, null!)
{
    [McpServerTool]
    [Description("Searches for NuGet packages by query and comma-separated keywords. Provides fast and precise search results.")]
    public Task<PackageSearchResult> search_packages(
        [Description("Description of the functionality you're looking for, or comma-separated keywords for targeted search")] string query,
        [Description("Maximum number of results to return (default: 20, max: 100)")] int maxResults = 20,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => SearchPackagesCore(query, maxResults, progressNotifier, cancellationToken),
            Logger,
            "Error searching packages");
    }

    private async Task<PackageSearchResult> SearchPackagesCore(
        string query,
        int maxResults,
        ProgressNotifier progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query cannot be empty", nameof(query));

        progress.ReportMessage("Direct search");

        // Extract keywords from comma-separated values
        var keywords = query.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Skip(1) // Skip the first one as it's used as the main query
            .ToList();

        progress.ReportMessage("Keyword search");

        return await searchService.SearchWithKeywordsAsync(query, keywords, maxResults, cancellationToken);
    }
}
