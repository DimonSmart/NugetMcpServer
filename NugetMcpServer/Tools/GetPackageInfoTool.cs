using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;
using NuGetMcpServer.Services.Formatters;
using NuGet.Packaging;

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

        List<string> libFiles;
        using (var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
        {
            libFiles = ExtractLibFiles(reader);
        }

        var versions = await PackageService.GetLatestVersions(packageId);

        return FormatPackageInfo(packageInfo, versions, libFiles);
    }

    private static string FormatPackageInfo(PackageInfo packageInfo, IReadOnlyList<string> versions, List<string> libFiles)
    {
        var sb = new StringBuilder();
        string header = $"Package: {packageInfo.PackageId} v{packageInfo.Version}";
        sb.AppendLine(header);
        sb.AppendLine(new string('=', header.Length));
        sb.AppendLine();

        bool showDependenciesLater = true;
        if (packageInfo.IsMetaPackage)
        {
            sb.Append(packageInfo.GetMetaPackageWarningIfAny());
            showDependenciesLater = false;
        }

        if (!string.IsNullOrWhiteSpace(packageInfo.Description))
        {
            sb.AppendLine($"Description: {packageInfo.Description}");
            sb.AppendLine();
        }

        if (packageInfo.Authors?.Count > 0)
        {
            sb.AppendLine($"Authors: {string.Join(", ", packageInfo.Authors)}");
        }

        if (packageInfo.Tags?.Count > 0)
        {
            sb.AppendLine($"Tags: {string.Join(", ", packageInfo.Tags)}");
        }

        if (!string.IsNullOrWhiteSpace(packageInfo.ProjectUrl))
        {
            sb.AppendLine($"Project URL: {packageInfo.ProjectUrl}");
        }

        if (!string.IsNullOrWhiteSpace(packageInfo.LicenseUrl))
        {
            sb.AppendLine($"License URL: {packageInfo.LicenseUrl}");
        }

        if (versions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Recent versions: {string.Join(", ", versions)}");
        }

        sb.AppendLine();
        sb.AppendLine($"LIB_FILES_COUNT: {libFiles.Count}");
        if (libFiles.Any())
        {
            sb.AppendLine("LIB_FILES:");
            foreach (var file in libFiles.Take(10))
            {
                sb.AppendLine($"  {file}");
            }
            if (libFiles.Count > 10)
            {
                sb.AppendLine($"  ... and {libFiles.Count - 10} more");
            }
        }

        if (showDependenciesLater)
        {
            if (packageInfo.Dependencies.Any())
            {
                sb.AppendLine();
                sb.AppendLine($"DEPENDENCIES_COUNT: {packageInfo.Dependencies.Count}");
                sb.AppendLine("DEPENDENCIES:");
                var uniqueDeps = packageInfo.Dependencies
                    .GroupBy(d => d.Id)
                    .Select(g => g.First())
                    .OrderBy(d => d.Id)
                    .Take(100)
                    .ToList();

                foreach (var dep in uniqueDeps)
                {
                    sb.AppendLine($"  - {dep.Id} ({dep.Version})");
                }

                if (packageInfo.Dependencies.Count > 100)
                {
                    sb.AppendLine($"  ... and {packageInfo.Dependencies.Count - 100} more");
                }
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("No dependencies.");
            }
        }

        sb.AppendLine();
        var recommendation = packageInfo.IsMetaPackage
            ? "Analyze the dependencies listed above to find actual implementations."
            : GenerateSmartRecommendations(packageInfo);
        sb.AppendLine($"RECOMMENDATION: {recommendation}");

        return sb.ToString();
    }

    private static List<string> ExtractLibFiles(PackageArchiveReader reader)
    {
        IEnumerable<string> files = reader.GetFiles();
        return files
            .Where(f => f.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("/"))
            .Where(f => !f.EndsWith("/_._", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("\\_._", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string GenerateSmartRecommendations(PackageInfo packageInfo)
    {
        var recommendations = new List<string>
        {
            "This package contains actual implementations. Use class/interface listing tools."
        };

        if (!packageInfo.Dependencies.Any())
        {
            return string.Join(" ", recommendations);
        }

        string[] packageNameParts = packageInfo.PackageId.Split('.');
        if (packageNameParts.Length >= 2)
        {
            string packagePrefix = string.Join(".", packageNameParts.Take(2));

            List<string> relatedDependencies = packageInfo.Dependencies
                .Where(d => d.Id.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(d.Id, packageInfo.PackageId, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.Id)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (relatedDependencies.Any())
            {
                recommendations.Add($"Related packages in the same family that may contain additional implementations: {string.Join(", ", relatedDependencies)}.");
            }
        }

        if (packageInfo.Dependencies.Count > 0)
        {
            recommendations.Add("Consider exploring dependencies for additional functionality.");
        }

        return string.Join(" ", recommendations);
    }
}
