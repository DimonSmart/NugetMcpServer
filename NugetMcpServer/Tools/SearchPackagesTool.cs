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
using NuGetMcpServer.Services;

using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class SearchPackagesTool(ILogger<SearchPackagesTool> logger, NuGetPackageService packageService) : McpToolBase<SearchPackagesTool>(logger, packageService)
{
    private static readonly string[] StopWords = ["алгоритм", "пакет", "math", "formula"];
    [McpServerTool]
    [Description("Searches for NuGet packages by description or functionality with optional AI-enhanced fuzzy search. For non-fuzzy search, you can provide comma-separated keywords for faster targeted search with balanced results.")]
    public Task<PackageSearchResult> SearchPackages(
        IMcpServer thisServer,
        [Description("Description of the functionality you're looking for, or comma-separated keywords for targeted search (fast option without fuzzy)")] string query,
        [Description("Maximum number of results to return (default: 20, max: 100)")] int maxResults = 20,
        [Description("Enable fuzzy search to include AI-generated package name alternatives (default: false)")] bool fuzzySearch = false,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithLoggingAsync(
            () => SearchPackagesCore(thisServer, query, maxResults, fuzzySearch, progress, cancellationToken),
            Logger,
            "Error searching packages");
    }
    private async Task<PackageSearchResult> SearchPackagesCore(
        IMcpServer thisServer,
        string query,
        int maxResults,
        bool fuzzySearch,
        IProgress<ProgressNotificationValue>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        if (maxResults <= 0 || maxResults > 100)
            maxResults = 100;

        Logger.LogInformation("Starting package search for query: {Query}, fuzzy: {FuzzySearch}", query, fuzzySearch);
        progress?.Report(new ProgressNotificationValue() { Progress = 10, Total = 100, Message = "Searching" });

        if (!fuzzySearch)
        {
            var keywords = query.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();

            var keywordResults = await SearchKeywordsAsync(keywords, maxResults, cancellationToken);
            var balanced = SearchResultBalancer.Balance(keywordResults, maxResults);

            progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = "Search complete" });

            return new PackageSearchResult
            {
                Query = query,
                TotalCount = balanced.Count,
                Packages = balanced,
                UsedAiKeywords = false,
                AiKeywords = string.Join(", ", keywords)
            };
        }

        var resultSets = new List<SearchResultSet>();

        // direct search
        var direct = await PackageService.SearchPackagesAsync(query, maxResults, null);
        resultSets.Add(new SearchResultSet(query, direct.ToList()));
        progress?.Report(new ProgressNotificationValue() { Progress = 30, Total = 100, Message = "Direct search complete" });

        // word-based search
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Where(w => !StopWords.Contains(w, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var wordResults = await SearchKeywordsAsync(words, maxResults, cancellationToken);
        resultSets.AddRange(wordResults);
        progress?.Report(new ProgressNotificationValue() { Progress = 60, Total = 100, Message = "Word search complete" });

        // AI suggestions
        var aiKeywords = await AIGeneratePackageNamesAsync(thisServer, query, 10, cancellationToken);
        aiKeywords = aiKeywords
            .Where(k => !StopWords.Contains(k, StringComparer.OrdinalIgnoreCase))
            .Where(k => !words.Contains(k, StringComparer.OrdinalIgnoreCase) && !k.Equals(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var aiResults = await SearchKeywordsAsync(aiKeywords, maxResults, cancellationToken);
        resultSets.AddRange(aiResults);
        progress?.Report(new ProgressNotificationValue() { Progress = 90, Total = 100, Message = "AI search complete" });

        var finalResults = SearchResultBalancer.Balance(resultSets, maxResults);
        progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = "Search complete" });

        return new PackageSearchResult
        {
            Query = query,
            TotalCount = finalResults.Count,
            Packages = finalResults,
            UsedAiKeywords = aiKeywords.Any(),
            AiKeywords = string.Join(", ", aiKeywords)
        };
    }

    private async Task<IReadOnlyCollection<string>> AIGeneratePackageNamesAsync(
        IMcpServer thisServer,
        string originalQuery,
        int packageCount,
        CancellationToken cancellationToken)
    {
        var prompts = new[]
        {
            PromptConstants.PackageSearchPrompt,
        };

        var allResults = new List<string>();
        var resultsPerPrompt = Math.Max(1, packageCount / prompts.Length);

        foreach (var promptTemplate in prompts)
        {
            try
            {
                var formattedPrompt = string.Format(promptTemplate, resultsPerPrompt, originalQuery);
                var names = await ExecuteSinglePromptAsync(thisServer, formattedPrompt, resultsPerPrompt, cancellationToken);
                allResults.AddRange(names);

                if (allResults.Count >= packageCount)
                    break;
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
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, prompt)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = expectedCount * 10,
            Temperature = 0.95f
        };

        var response = await thisServer
            .AsSamplingChatClient()
            .GetResponseAsync(messages, options, cancellationToken);

        return response.ToString()
            .Split(new[] { "\r", "\n", "," }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(expectedCount);
    }

    private async Task<List<SearchResultSet>> SearchKeywordsAsync(
        IReadOnlyCollection<string> keywords,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResultSet>();

        foreach (var keyword in keywords)
        {
            try
            {
                var packages = await PackageService.SearchPackagesAsync(keyword, maxResults, null);
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
