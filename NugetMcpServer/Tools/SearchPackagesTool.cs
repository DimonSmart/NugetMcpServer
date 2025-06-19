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

        progress?.Report(new ProgressNotificationValue() { Progress = 5, Total = 100, Message = "Starting package search" });
        
        if (!fuzzySearch)
        {
            var keywords = query.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();

            Logger.LogInformation("Performing balanced search with {Count} keywords: {Keywords}", keywords.Count, string.Join(", ", keywords));
            progress?.Report(new ProgressNotificationValue() { Progress = 20, Total = 100, Message = $"Balanced search with {keywords.Count} keyword(s)" });
            
            var balancedResults = await SearchWithBalancedResultsAsync(keywords, maxResults, progress, cancellationToken);
            progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = $"Balanced search completed - Found {balancedResults.Count} packages" });
            
            return new PackageSearchResult
            {
                Query = query,
                TotalCount = balancedResults.Count,
                Packages = balancedResults.Take(maxResults).ToList(),
                UsedAiKeywords = false,
                AiKeywords = $"Balanced search keywords: {string.Join(", ", keywords)}"
            };
        }

        // Phase 1: Direct search as fallback
        var initialResults = await PackageService.SearchPackagesAsync(query, maxResults, progress);
        Logger.LogInformation("Initial search found {Count} packages", initialResults.Count);
        progress?.Report(new ProgressNotificationValue() { Progress = 40, Total = 100, Message = "Generating AI package names" });

        // Phase 2: Fuzzy search - enhance with AI-generated package name alternatives
        var aiPackageNames = await AIGeneratePackageNamesAsync(thisServer, query, 10, cancellationToken);
        if (!aiPackageNames.Any())
        {
            Logger.LogWarning("AI package name generation failed or returned empty result");
            progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = "AI generation failed, using direct results" });
            return new PackageSearchResult
            {
                Query = query,
                TotalCount = initialResults.Count,
                Packages = initialResults.Take(maxResults).ToList(),
                UsedAiKeywords = false
            };
        }

        Logger.LogInformation("Generated AI package names: {PackageNames}", string.Join(", ", aiPackageNames));
        progress?.Report(new ProgressNotificationValue() { Progress = 60, Total = 100, Message = $"Searching with AI-generated keywords: {string.Join(", ", aiPackageNames)}" });

        var balancedAiResults = await SearchWithBalancedResultsAsync(aiPackageNames, maxResults, progress, cancellationToken);
        Logger.LogInformation("AI-enhanced fuzzy search found {Count} balanced packages", balancedAiResults.Count);
        progress?.Report(new ProgressNotificationValue() { Progress = 85, Total = 100, Message = "Combining and deduplicating results" });

        // Combine results, removing duplicates by package ID and sorting by popularity
        var combinedResults = initialResults
            .Concat(balancedAiResults)
            .GroupBy(p => p.Id.ToLowerInvariant())
            .Select(g => g.First())
            .OrderByDescending(p => p.DownloadCount)
            .Take(maxResults)
            .ToList();

        progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = $"Search completed - Found {combinedResults.Count} packages" });

        return new PackageSearchResult
        {
            Query = query,
            TotalCount = combinedResults.Count,
            Packages = combinedResults,
            UsedAiKeywords = true,
            AiKeywords = string.Join(", ", aiPackageNames)
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

    private async Task<IReadOnlyCollection<Services.PackageInfo>> SearchWithBalancedResultsAsync(
        IReadOnlyCollection<string> keywords,
        int maxTotalResults,
        IProgress<ProgressNotificationValue>? progress,
        CancellationToken cancellationToken)
    {
        if (!keywords.Any())
            return new List<Services.PackageInfo>();

        var keywordResults = new Dictionary<string, List<Services.PackageInfo>>();
        var resultsPerKeyword = Math.Max(1, maxTotalResults / keywords.Count);
        var remainingSlots = maxTotalResults % keywords.Count;

        var keywordList = keywords.ToList();
        var processedKeywords = 0;

        // Search for each keyword separately
        foreach (var keyword in keywordList)
        {
            try
            {
                progress?.Report(new ProgressNotificationValue() { Progress = (float)(60 + (processedKeywords * 20.0 / keywordList.Count)), Total = 100, Message = $"Searching for keyword: {keyword}" });

                var results = await PackageService.SearchPackagesAsync(keyword, resultsPerKeyword + 10, progress);
                keywordResults[keyword] = results.ToList();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to search packages for keyword: {Keyword}", keyword);
                keywordResults[keyword] = new List<Services.PackageInfo>();
            }
            processedKeywords++;
        }

        // Balance the results - take equal amounts from each keyword
        var balancedResults = new List<Services.PackageInfo>();
        var usedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: take equal amounts from each keyword
        for (var i = 0; i < resultsPerKeyword; i++)
        {
            foreach (var keyword in keywords)
            {
                if (!keywordResults.TryGetValue(keyword, out var results) || i >= results.Count)
                    continue;

                var package = results[i];
                if (usedPackageIds.Add(package.Id))
                {
                    balancedResults.Add(package);

                    if (balancedResults.Count >= maxTotalResults)
                        return balancedResults;
                }
            }
        }

        // Second pass: distribute remaining slots to keywords with more results
        var keywordsWithMoreResults = keywordResults
            .Where(kv => kv.Value.Count > resultsPerKeyword)
            .OrderByDescending(kv => kv.Value.Count)
            .ToList();

        var currentKeywordIndex = 0;
        while (balancedResults.Count < maxTotalResults && remainingSlots > 0 && keywordsWithMoreResults.Any())
        {
            var keywordPair = keywordsWithMoreResults[currentKeywordIndex % keywordsWithMoreResults.Count];
            var results = keywordPair.Value;

            for (var i = resultsPerKeyword; i < results.Count && balancedResults.Count < maxTotalResults && remainingSlots > 0; i++)
            {
                var package = results[i];
                if (usedPackageIds.Add(package.Id))
                {
                    balancedResults.Add(package);
                    remainingSlots--;
                    break;
                }
            }

            currentKeywordIndex++;
        }

        Logger.LogInformation("Balanced search results: {TotalResults} packages from {KeywordCount} keywords. Results per keyword: {ResultsPerKeyword}, remaining slots distributed: {RemainingSlots}",
            balancedResults.Count, keywords.Count, resultsPerKeyword, maxTotalResults % keywords.Count);

        return balancedResults;
    }

}
