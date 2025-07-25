using System;
using System.Collections.Generic;
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
public class ListInterfacesTool(ILogger<ListInterfacesTool> logger, NuGetPackageService packageService, ArchiveProcessingService archiveProcessingService) : McpToolBase<ListInterfacesTool>(logger, packageService)
{
    private readonly ArchiveProcessingService _archiveProcessingService = archiveProcessingService;
    [McpServerTool]
    [Description("Lists all public interfaces available in a specified NuGet package.")]
    public Task<InterfaceListResult> list_interfaces(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => ListInterfacesCore(packageId, version, progressNotifier),
            Logger,
            "Error listing interfaces");
    }

    private async Task<InterfaceListResult> ListInterfacesCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        Logger.LogInformation("Listing interfaces from package {PackageId} version {Version}", packageId, version!);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        var result = new InterfaceListResult
        {
            PackageId = packageId,
            Version = version!,
            Interfaces = new List<InterfaceInfo>()
        };

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for interfaces");
        packageStream.Position = 0;
        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);

        var loadedAssemblies = _archiveProcessingService.LoadAllAssembliesFromPackage(packageReader);

        foreach (var assemblyInfo in loadedAssemblies)
        {
            Logger.LogInformation("Processing archive entry: {AssemblyName}", assemblyInfo.AssemblyName);

            var interfaces = assemblyInfo.Types
                .Where(t => t.IsInterface && t.IsPublic)
                .ToList();

            Logger.LogInformation("Found {InterfaceCount} interfaces in {AssemblyName}", interfaces.Count, assemblyInfo.AssemblyName);

            foreach (var iface in interfaces)
            {
                Logger.LogDebug("Found interface: {InterfaceName} ({FullName})", iface.Name, iface.FullName);
                result.Interfaces.Add(new InterfaceInfo
                {
                    Name = iface.Name,
                    FullName = iface.FullName ?? string.Empty,
                    AssemblyName = assemblyInfo.AssemblyName
                });
            }
        }

        progress.ReportMessage($"Interface listing completed - Found {result.Interfaces.Count} interfaces");

        return result;
    }

}
