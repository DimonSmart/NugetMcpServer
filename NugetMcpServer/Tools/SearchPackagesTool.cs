using System;
using System.Collections.Generic;
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
public class SearchPackagesTool(ILogger<SearchPackagesTool> logger, NuGetPackageService packageService) : McpToolBase<SearchPackagesTool>(logger, packageService)
{
    private sealed class SearchContext
    {
        public HashSet<string> Keywords { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SearchResultSet> Sets { get; } = [];

        public void Add(string keyword, IEnumerable<PackageInfo> packages)
        {
            Keywords.Add(keyword);
            Sets.Add(new SearchResultSet(keyword, packages.ToList()));
        }
    }

    [McpServerTool]
    [Description("Searches for NuGet packages by query and comma-separated keywords. Provides fast and precise search results.")]
    public Task<PackageSearchResult> SearchPackages(
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

        if (maxResults <= 0 || maxResults > 100) maxResults = 100;

        Logger.LogInformation("Starting package search for query: {Query}", query);

        var ctx = new SearchContext();

        // Direct search - always performed without stop words filtering!
        ctx.Add(query, await PackageService.SearchPackagesAsync(query, maxResults));

        progress.ReportMessage("Direct search");

        // Keyword search from comma-separated values, filtered by stop words and duplicates
        var keywords = query.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Where(k => !StopWords.Words.Contains(k, StringComparer.OrdinalIgnoreCase))
            .Where(k => !ctx.Keywords.Contains(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Any())
        {
            ctx.Keywords.UnionWith(keywords);
            var keywordResults = await SearchKeywordsAsync(keywords, maxResults, cancellationToken);
            ctx.Sets.AddRange(keywordResults);
        }

        progress.ReportMessage("Keyword search");

        var balanced = SearchResultBalancer.Balance(ctx.Sets, maxResults);
        return new PackageSearchResult
        {
            Query = query,
            TotalCount = balanced.Count,
            Packages = balanced
        };
    }

    private async Task<List<SearchResultSet>> SearchKeywordsAsync(IReadOnlyCollection<string> keywords, int maxResults, CancellationToken cancellationToken)
    {
        List<SearchResultSet> results = [];

        foreach (string keyword in keywords)
        {
            try
            {
                var packages = await PackageService.SearchPackagesAsync(keyword, maxResults);
                results.Add(new SearchResultSet(keyword, packages.ToList()));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to search packages for keyword: {Keyword}", keyword);
                results.Add(new SearchResultSet(keyword, []));
            }
        }

        return results;
    }
}
