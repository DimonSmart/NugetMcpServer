using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuGetMcpServer.Common;

namespace NuGetMcpServer.Services;

public class PackageSearchService(ILogger<PackageSearchService> logger, NuGetPackageService packageService)
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

    public async Task<PackageSearchResult> SearchWithKeywordsAsync(
        string query,
        IEnumerable<string> additionalKeywords,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) 
            throw new ArgumentException("Query cannot be empty", nameof(query));

        if (maxResults <= 0 || maxResults > 100) 
            maxResults = 100;

        logger.LogInformation("Starting package search for query: {Query}", query);

        var ctx = new SearchContext();

        // Direct search as baseline
        ctx.Add(query, await packageService.SearchPackagesAsync(query, maxResults));

        // Additional keywords search - filtered by stop words and duplicates
        var filteredKeywords = additionalKeywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Where(k => !StopWords.Words.Contains(k, StringComparer.OrdinalIgnoreCase))
            .Where(k => !ctx.Keywords.Contains(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (filteredKeywords.Any())
        {
            ctx.Keywords.UnionWith(filteredKeywords);
            var keywordResults = await SearchKeywordsAsync(filteredKeywords, maxResults, cancellationToken);
            ctx.Sets.AddRange(keywordResults);
        }

        var balanced = SearchResultBalancer.Balance(ctx.Sets, maxResults);
        return new PackageSearchResult
        {
            Query = query,
            TotalCount = balanced.Count,
            Packages = balanced
        };
    }

    private async Task<List<SearchResultSet>> SearchKeywordsAsync(
        IReadOnlyCollection<string> keywords, 
        int maxResults, 
        CancellationToken cancellationToken)
    {
        List<SearchResultSet> results = [];

        foreach (string keyword in keywords)
        {
            try
            {
                var packages = await packageService.SearchPackagesAsync(keyword, maxResults);
                results.Add(new SearchResultSet(keyword, packages.ToList()));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search packages for keyword: {Keyword}", keyword);
                results.Add(new SearchResultSet(keyword, []));
            }
        }

        return results;
    }
}
