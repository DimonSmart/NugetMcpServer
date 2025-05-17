using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class ListInterfacesTool : McpToolBase<ListInterfacesTool>
{
    public ListInterfacesTool(ILogger<ListInterfacesTool> logger, NuGetPackageService packageService)
        : base(logger, packageService)
    {
    }
    [McpServerTool]
    [Description(
       "Lists all public interfaces available in a specified NuGet package. " +
       "Parameters: " +
       "packageId — NuGet package ID; " +
       "version (optional) — package version (defaults to latest). " +
       "Returns package ID, version and list of interfaces."
    )]
    public Task<InterfaceListResult> ListInterfaces(
        string packageId,
        string? version = null)
    {
        return ExecuteWithLoggingAsync(
            () => ListInterfacesCore(packageId, version),
            Logger,
            "Error listing interfaces");
    }

    private async Task<InterfaceListResult> ListInterfacesCore(string packageId, string? version)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        // Ensure we have non-null values for packageId and version
        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Listing interfaces from package {PackageId} version {Version}",
            packageId, version);

        var result = new InterfaceListResult
        {
            PackageId = packageId,
            Version = version,
            Interfaces = new List<InterfaceInfo>()
        };

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        // Scan each DLL in the package
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            ProcessArchiveEntry(entry, result);
        }

        return result;
    }

    private void ProcessArchiveEntry(ZipArchiveEntry entry, InterfaceListResult result)
    {
        try
        {
            // Read the DLL into memory
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
