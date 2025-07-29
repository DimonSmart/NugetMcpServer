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

        var (loaded, packageInfo, resolvedVersion) =
            await _archiveProcessingService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Listing interfaces from package {PackageId} version {Version}",
            packageId, resolvedVersion);

        var result = new InterfaceListResult
        {
            PackageId = packageId,
            Version = resolvedVersion,
            Interfaces = new List<InterfaceInfo>()
        };

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for interfaces");

        foreach (var assemblyInfo in loaded.Assemblies)
        {
            Logger.LogInformation("Processing archive entry: {AssemblyName}", assemblyInfo.FileName);

            var interfaces = assemblyInfo.Types
                .Where(t => t.IsInterface && t.IsPublic)
                .ToList();

            Logger.LogInformation("Found {InterfaceCount} interfaces in {AssemblyName}", interfaces.Count, assemblyInfo.FileName);

            foreach (var iface in interfaces)
            {
                Logger.LogDebug("Found interface: {InterfaceName} ({FullName})", iface.Name, iface.FullName);
                result.Interfaces.Add(new InterfaceInfo
                {
                    Name = iface.Name,
                    FullName = iface.FullName ?? string.Empty,
                    AssemblyName = assemblyInfo.FileName
                });
            }
        }

        progress.ReportMessage($"Interface listing completed - Found {result.Interfaces.Count} interfaces");

        return result;
    }

}
