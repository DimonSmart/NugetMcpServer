using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.AI;
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
    [Description("Searches for NuGet packages. The query is always searched directly. Without fuzzy search you can also provide comma-separated keywords. Fuzzy search extends the basic search with word and AI generated name matching.")]
    public Task<PackageSearchResult> SearchPackages(
        IMcpServer thisServer,
        [Description("Description of the functionality you're looking for, or comma-separated keywords for targeted search (fast option without fuzzy)")] string query,
        [Description("Maximum number of results to return (default: 20, max: 100)")] int maxResults = 20,
        [Description("Enable fuzzy search to include AI-generated package name alternatives (default: false)")] bool fuzzySearch = false,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => SearchPackagesCore(thisServer, query, maxResults, fuzzySearch, progressNotifier, cancellationToken),
            Logger,
            "Error searching packages");
    }

    private async Task<PackageSearchResult> SearchPackagesCore(
        IMcpServer thisServer,
        string query,
        int maxResults,
        bool fuzzySearch,
        ProgressNotifier progress,
        CancellationToken cancellationToken)
    {


        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query cannot be empty", nameof(query));

        if (maxResults <= 0 || maxResults > 100) maxResults = 100;

        Logger.LogInformation("Starting package search for query: {Query}, fuzzy: {FuzzySearch}", query, fuzzySearch);

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
        
        if (!fuzzySearch)
        {
            var balanced = SearchResultBalancer.Balance(ctx.Sets, maxResults);
            return new PackageSearchResult
            {
                Query = query,
                TotalCount = balanced.Count,
                Packages = balanced
            };
        }

        // Word search from space-separated values, filtered by stop words and duplicates
        var words = keywords.SelectMany(k => k.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(w => w.Trim())
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Where(w => !StopWords.Words.Contains(w, StringComparer.OrdinalIgnoreCase))
            .Where(w => !ctx.Keywords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (words.Any())
        {
            ctx.Keywords.UnionWith(words);
            var wordResults = await SearchKeywordsAsync(words, maxResults, cancellationToken);
            ctx.Sets.AddRange(wordResults);
        }
        progress.ReportMessage("Word search");

        // AI suggestions - filtered by stop words and duplicates
        var aiKeywords = await AIGeneratePackageNamesAsync(thisServer, query, 10, cancellationToken);
        var filteredAi = aiKeywords
            .Where(k => !StopWords.Words.Contains(k, StringComparer.OrdinalIgnoreCase))
            .Where(k => !ctx.Keywords.Contains(k))
            .ToList();

        if (filteredAi.Any())
        {
            ctx.Keywords.UnionWith(filteredAi);
            var aiResults = await SearchKeywordsAsync(filteredAi, maxResults, cancellationToken);
            ctx.Sets.AddRange(aiResults);
        }

        progress.ReportMessage("AI search");
        var finalResults = SearchResultBalancer.Balance(ctx.Sets, maxResults);
        
        return new PackageSearchResult
        {
            Query = query,
            TotalCount = finalResults.Count,
            Packages = finalResults
        };
    }

    private async Task<IReadOnlyCollection<string>> AIGeneratePackageNamesAsync(
        IMcpServer thisServer,
        string originalQuery,
        int packageCount,
        CancellationToken cancellationToken)
    {
        string[] prompts =
        [
            PromptConstants.PackageSearchPrompt,
        ];

        var allResults = new List<string>();
        int resultsPerPrompt = Math.Max(1, packageCount / prompts.Length);

        foreach (string? promptTemplate in prompts)
        {
            try
            {
                string formattedPrompt = string.Format(promptTemplate, resultsPerPrompt, originalQuery);
                IEnumerable<string> names = await ExecuteSinglePromptAsync(thisServer, formattedPrompt, resultsPerPrompt, cancellationToken);
                allResults.AddRange(names);

                if (allResults.Count >= packageCount)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to execute prompt for query: {Query}", originalQuery);
            }
        }

        return allResults
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(packageCount)
            .ToList()
            .AsReadOnly();
    }

    private async Task<IEnumerable<string>> ExecuteSinglePromptAsync(
        IMcpServer thisServer,
        string prompt,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        ChatMessage[] messages =
        [
            new ChatMessage(ChatRole.User, prompt)
        ];

        ChatOptions options = new ChatOptions
        {
            MaxOutputTokens = expectedCount * 10,
            Temperature = 0.95f
        };

        ChatResponse response = await thisServer
            .AsSamplingChatClient()
            .GetResponseAsync(messages, options, cancellationToken);

        return response.ToString()
            .Split(["\r", "\n", ","], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(expectedCount);
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
