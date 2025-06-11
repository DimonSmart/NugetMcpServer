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
        return ExecuteWithLoggingAsync(
            () => ListInterfacesCore(packageId, version, progress),
            Logger,
            "Error listing interfaces");
    }
    private async Task<InterfaceListResult> ListInterfacesCore(string packageId, string? version, IProgress<ProgressNotificationValue>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        progress?.Report(new ProgressNotificationValue() { Progress = 10, Total = 100, Message = "Resolving package version" });

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        // Ensure we have non-null values for packageId and version
        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Listing interfaces from package {PackageId} version {Version}",
            packageId, version);

        progress?.Report(new ProgressNotificationValue() { Progress = 30, Total = 100, Message = $"Downloading package {packageId} v{version}" });

        var result = new InterfaceListResult
        {
            PackageId = packageId,
            Version = version,
            Interfaces = new List<InterfaceInfo>()
        };

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress?.Report(new ProgressNotificationValue() { Progress = 70, Total = 100, Message = "Scanning assemblies for interfaces" });

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            ProcessArchiveEntry(entry, result);
        }

        progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = $"Interface listing completed - Found {result.Interfaces.Count} interfaces" });

        return result;
    }

    private void ProcessArchiveEntry(ZipArchiveEntry entry, InterfaceListResult result)
    {
        try
        {
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);

            var assemblyData = ms.ToArray();
            var assembly = PackageService.LoadAssemblyFromMemory(assemblyData);

            if (assembly == null) return;

            var assemblyName = Path.GetFileName(entry.FullName);
            var interfaces = assembly.GetTypes()
                .Where(t => t.IsInterface && t.IsPublic)
                .ToList();

            foreach (var iface in interfaces)
            {
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
            Logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
        }
    }
}
