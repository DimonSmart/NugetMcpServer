using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;
using NuGetMcpServer.Services.Formatters;

using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class GetPackageInfoTool(
    ILogger<GetPackageInfoTool> logger,
    NuGetPackageService packageService) : McpToolBase<GetPackageInfoTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Gets detailed information about a NuGet package including metadata, dependencies, and whether it's a meta-package.")]
    public Task<string> get_package_info(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetPackageInfoCore(packageId, version, progressNotifier),
            Logger,
            "Error getting package information");
    }

    private async Task<string> GetPackageInfoCore(
        string packageId,
        string? version,
        ProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        Logger.LogInformation("Getting information for package {PackageId} version {Version}", packageId, version!);

        progress.ReportMessage($"Downloading package {packageId} v{version}");
        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);

        var versions = await PackageService.GetLatestVersions(packageId);

        return FormatPackageInfo(packageInfo, versions);
    }

    private static string FormatPackageInfo(PackageInfo packageInfo, IReadOnlyList<string> versions)
    {
        var result = $"Package: {packageInfo.PackageId} v{packageInfo.Version}\n";
        result += new string('=', result.Length - 1) + "\n\n";
        result += packageInfo.GetMetaPackageWarningIfAny();

        if (!string.IsNullOrWhiteSpace(packageInfo.Description))
        {
            result += $"Description: {packageInfo.Description}\n\n";
        }

        if (packageInfo.Authors?.Count > 0)
        {
            result += $"Authors: {string.Join(", ", packageInfo.Authors)}\n";
        }

        if (packageInfo.Tags?.Count > 0)
        {
            result += $"Tags: {string.Join(", ", packageInfo.Tags)}\n";
        }

        if (!string.IsNullOrWhiteSpace(packageInfo.ProjectUrl))
        {
            result += $"Project URL: {packageInfo.ProjectUrl}\n";
        }

        if (!string.IsNullOrWhiteSpace(packageInfo.LicenseUrl))
        {
            result += $"License URL: {packageInfo.LicenseUrl}\n";
        }

        if (versions.Count > 0)
        {
            var orderedVersions = versions.Reverse();
            result += $"\nRecent versions: {string.Join(", ", orderedVersions)}\n";
        }

        if (packageInfo.Dependencies.Count == 0)
        {
            result += "\nNo dependencies.\n";
        }
        else
        {
            result += "\nDependencies:\n";

            var uniqueDeps = packageInfo.Dependencies
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderBy(d => d.Id)
                .ToList();

            foreach (var dep in uniqueDeps)
            {
                result += $"  - {dep.Id} ({dep.Version})\n";
            }
        }

        return result;
    }
}
