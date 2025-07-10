using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using NuGet.Packaging;

using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;

using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class GetPackageDependenciesTool(
    ILogger<GetPackageDependenciesTool> logger,
    NuGetPackageService packageService) : McpToolBase<GetPackageDependenciesTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Gets dependencies of a NuGet package to help understand what other packages contain the actual implementations.")]
    public Task<string> GetPackageDependencies(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetPackageDependenciesCore(packageId, version, progressNotifier),
            Logger,
            "Error getting package dependencies");
    }

    private async Task<string> GetPackageDependenciesCore(
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

        Logger.LogInformation("Getting dependencies for package {PackageId} version {Version}",
            packageId, version!);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Analyzing package dependencies");

        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);
        var dependencies = PackageService.GetPackageDependencies(packageStream);

        var result = $"/* DEPENDENCIES FOR {packageId} v{version} */\n\n";
        result += $"Title: {packageInfo.PackageId}\n";
        result += $"Description: {packageInfo.Description}\n\n";

        if (dependencies.Count == 0)
        {
            result += "This package has no dependencies.\n";
        }
        else
        {
            result += $"This package has {dependencies.Count} dependencies:\n\n";

            var uniqueDependencies = dependencies
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderBy(d => d.Id)
                .ToList();

            foreach (var dep in uniqueDependencies)
            {
                result += $"  - {dep.Id} ({dep.Version})\n";
            }

            if (dependencies.Count > 0)
            {
                result += "\nTo explore the actual implementations, try listing classes/interfaces from these dependencies:\n";
                foreach (var dep in uniqueDependencies.Take(3))
                {
                    result += $"  - NuGet_list_classes(packageId=\"{dep.Id}\")\n";
                }
            }
        }

        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        var dllFiles = ArchiveProcessingService.GetUniqueAssemblyFiles(packageReader);
        var hasOnlySmallDlls = dllFiles.All(f =>
        {
            try
            {
                using var stream = packageReader.GetStream(f);
                return stream.Length < 50000;
            }
            catch
            {
                return false;
            }
        });

        if (dependencies.Count > 0 && hasOnlySmallDlls && dllFiles.Any())
        {
            result += "\nNOTE: This appears to be a meta-package that primarily serves to group related packages together.\n";
            result += "The actual functionality is implemented in the dependencies listed above.\n";
        }

        progress.ReportMessage("Dependencies analysis completed");

        return result;
    }
}
