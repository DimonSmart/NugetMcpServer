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
public class SearchPackagesFuzzyTool(ILogger<SearchPackagesFuzzyTool> logger, PackageSearchService searchService) : McpToolBase<SearchPackagesFuzzyTool>(logger, null!)
{
    [McpServerTool]
    [Description("Advanced fuzzy search for NuGet packages using AI-generated alternatives and word matching. Use this method when regular search doesn't return desired results. This method uses sampling and may provide broader but less precise results.")]
    public Task<PackageSearchResult> search_packages_fuzzy(
        IMcpServer thisServer,
        [Description("Description of the functionality you're looking for")] string query,
        [Description("Maximum number of results to return (default: 20, max: 100)")] int maxResults = 20,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => SearchPackagesFuzzyCore(thisServer, query, maxResults, progressNotifier, cancellationToken),
            Logger,
            "Error performing fuzzy search for packages");
    }

    private async Task<PackageSearchResult> SearchPackagesFuzzyCore(
        IMcpServer thisServer,
        string query,
        int maxResults,
        ProgressNotifier progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Query cannot be empty", nameof(query));
        ArgumentNullException.ThrowIfNull(thisServer);

        Logger.LogInformation("Starting fuzzy package search for query: {Query}", query);

        progress.ReportMessage("Direct search");

        // AI suggestions - filtered by stop words and duplicates
        IReadOnlyCollection<string> aiKeywords = await AIGeneratePackageNamesAsync(thisServer, query, 10, cancellationToken);

        progress.ReportMessage("AI search");

        return await searchService.SearchWithKeywordsAsync(query, aiKeywords, maxResults, cancellationToken);
    }

    private async Task<IReadOnlyCollection<string>> AIGeneratePackageNamesAsync(
        IMcpServer thisServer,
        string originalQuery,
        int packageCount,
        CancellationToken cancellationToken)
    {
        List<string> allResults = new List<string>();

        try
        {
            string formattedPrompt = string.Format(PromptConstants.PackageSearchPrompt, 20, originalQuery);

            ChatMessage[] messages = [new ChatMessage(ChatRole.User, formattedPrompt)];

            ChatOptions options = new()
            {
                MaxOutputTokens = 20 * 5,
                Temperature = 0.95f
            };

            ChatResponse response = await thisServer
                .AsSamplingChatClient()
                .GetResponseAsync(messages, options, cancellationToken);

            IEnumerable<string> names = response.ToString()
                .Split(["\r", "\n", ","], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(20);

            allResults.AddRange(names);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to execute prompt for query: {Query}", originalQuery);
        }

        return allResults
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(packageCount)
            .ToList()
            .AsReadOnly();
    }
}
