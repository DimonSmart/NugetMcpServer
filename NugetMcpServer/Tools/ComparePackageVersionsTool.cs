using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;
using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

/// <summary>
/// MCP tool for comparing two versions of a NuGet package
/// </summary>
[McpServerToolType]
public class ComparePackageVersionsTool : McpToolBase<ComparePackageVersionsTool>
{
    private readonly PackageComparisonService _comparisonService;

    public ComparePackageVersionsTool(
        ILogger<ComparePackageVersionsTool> logger,
        NuGetPackageService packageService,
        PackageComparisonService comparisonService)
        : base(logger, packageService)
    {
        _comparisonService = comparisonService;
    }

    [McpServerTool]
    [Description("Compares two versions of a NuGet package and detects all API changes including breaking changes, additions, and removals.")]
    public Task<ComparisonResult> compare_package_versions(
        [Description("NuGet package ID")] string packageId,
        [Description("Older version to compare from")] string fromVersion,
        [Description("Newer version to compare to")] string toVersion,
        [Description("Optional filter for TYPE NAMES (classes, structs, records, interfaces). Supports wildcards: '*Controller' matches types ending with Controller, '*Message*' matches types containing Message. NOTE: This filters by type names, NOT by field/property names.")] string? typeNameFilter = null,
        [Description("Optional filter for MEMBER NAMES (properties, fields, methods). Supports wildcards: '*Id' matches members ending with Id, 'Calculate*' matches members starting with Calculate. Supports OR via '|': 'StarCount|TopicId'. Works independently from typeNameFilter.")] string? memberNameFilter = null,
        [Description("If true, returns only breaking changes (high severity). Non-breaking changes and additions are excluded. Default: false")] bool breakingChangesOnly = false,
        [Description("Maximum changes to return per category (default: 100)")] int maxChangesPerCategory = 100,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => CompareVersionsCore(packageId, fromVersion, toVersion, typeNameFilter, memberNameFilter, breakingChangesOnly, maxChangesPerCategory, progressNotifier),
            Logger,
            "Error comparing package versions");
    }

    private async Task<ComparisonResult> CompareVersionsCore(
        string packageId,
        string fromVersion,
        string toVersion,
        string? typeNameFilter,
        string? memberNameFilter,
        bool breakingChangesOnly,
        int maxChangesPerCategory,
        IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        if (string.IsNullOrWhiteSpace(fromVersion))
            throw new ArgumentNullException(nameof(fromVersion));

        if (string.IsNullOrWhiteSpace(toVersion))
            throw new ArgumentNullException(nameof(toVersion));

        Logger.LogInformation(
            "Comparing package {PackageId} versions {FromVersion} -> {ToVersion}",
            packageId, fromVersion, toVersion);

        progress.ReportMessage($"Starting comparison of {packageId} v{fromVersion} -> v{toVersion}");

        var result = await _comparisonService.CompareVersionsAsync(
            packageId,
            fromVersion,
            toVersion,
            typeNameFilter,
            memberNameFilter,
            breakingChangesOnly,
            maxChangesPerCategory,
            System.Threading.CancellationToken.None);

        progress.ReportMessage(
            $"Comparison complete: {result.Summary.TotalChanges} changes detected " +
            $"({result.Summary.BreakingChanges} breaking, {result.Summary.Additions} additions, {result.Summary.Removals} removals)");

        return result;
    }
}
