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

[McpServerToolType]
public class GetPackageInfoTool(
    ILogger<GetPackageInfoTool> logger,
    NuGetPackageService packageService) : McpToolBase<GetPackageInfoTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Gets detailed information about a NuGet package including metadata, dependencies, and whether it's a meta-package.")]
    public Task<string> GetPackageInfo(
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

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Getting information for package {PackageId} version {Version}", packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");
        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version);

        return FormatPackageInfo(packageInfo);
    }

    private static string FormatPackageInfo(PackageInfo packageInfo)
    {
        var result = $"Package: {packageInfo.Id} v{packageInfo.Version}\n";
        result += new string('=', result.Length - 1) + "\n\n";

        if (packageInfo.IsMetaPackage)
        {
            result += "⚠️  META-PACKAGE\n";
            result += "This package groups other packages together and may not contain actual implementation code.\n\n";
        }

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

        if (packageInfo.Dependencies.Count > 0)
        {
            result += "\nDependencies:\n";
            foreach (var dependency in packageInfo.Dependencies)
            {
                result += $"  • {dependency.Id} ({dependency.Version})\n";
            }
        }
        else
        {
            result += "\nNo dependencies.\n";
        }

        if (packageInfo.IsMetaPackage && packageInfo.Dependencies.Count > 0)
        {
            result += "\n💡 To see actual classes and interfaces, analyze one of the dependency packages listed above.\n";
        }

        return result;
    }
}
