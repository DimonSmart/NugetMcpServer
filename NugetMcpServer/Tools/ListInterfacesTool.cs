using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
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
public class ListInterfacesTool(ILogger<ListInterfacesTool> logger, NuGetPackageService packageService) : McpToolBase<ListInterfacesTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Lists all public interfaces available in a specified NuGet package.")]
    public Task<InterfaceListResult> ListInterfaces(
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

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Listing interfaces from package {PackageId} version {Version}", packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        var result = new InterfaceListResult
        {
            PackageId = packageId,
            Version = version,
            Interfaces = new List<InterfaceInfo>()
        };

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version);

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for interfaces");
        packageStream.Position = 0;
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        var dllEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();

        // Filter to avoid duplicate DLLs from different target frameworks
        var uniqueDllEntries = FilterUniqueAssemblies(dllEntries);

        foreach (var entry in uniqueDllEntries)
        {
            ProcessArchiveEntry(entry, result);
        }

        progress.ReportMessage($"Interface listing completed - Found {result.Interfaces.Count} interfaces");

        return result;
    }

    private void ProcessArchiveEntry(ZipArchiveEntry entry, InterfaceListResult result)
    {
        try
        {
            Logger.LogInformation("Processing archive entry: {EntryName}", entry.FullName);

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);

            var assemblyData = ms.ToArray();
            Logger.LogInformation("Archive entry {EntryName} size: {Size} bytes", entry.FullName, assemblyData.Length);

            var (assembly, types) = PackageService.LoadAssemblyFromMemoryWithTypes(assemblyData);

            if (assembly == null) return;

            var assemblyName = Path.GetFileName(entry.FullName);
            var interfaces = types
                .Where(t => t.IsInterface && t.IsPublic)
                .ToList();

            Logger.LogInformation("Found {InterfaceCount} interfaces in {AssemblyName}", interfaces.Count, assemblyName);

            foreach (var iface in interfaces)
            {
                Logger.LogDebug("Found interface: {InterfaceName} ({FullName})", iface.Name, iface.FullName);
                result.Interfaces.Add(new InterfaceInfo
                {
                    Name = iface.Name,
                    FullName = iface.FullName ?? string.Empty,
                    AssemblyName = assemblyName
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing archive entry {EntryName}", entry.FullName);
        }
    }
}
