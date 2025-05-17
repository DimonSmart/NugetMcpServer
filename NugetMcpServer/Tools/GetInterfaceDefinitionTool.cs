using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class GetInterfaceDefinitionTool : McpToolBase<GetInterfaceDefinitionTool>
{
    private readonly InterfaceFormattingService _formattingService;

    public GetInterfaceDefinitionTool(
        ILogger<GetInterfaceDefinitionTool> logger,
        NuGetPackageService packageService,
        InterfaceFormattingService formattingService)
        : base(logger, packageService)
    {
        _formattingService = formattingService;
    }

    /// <summary>
    /// Extracts and returns the C# interface definition from a specified NuGet package.
    /// </summary>
    /// <param name="packageId">
    ///   The NuGet package ID (exactly as on nuget.org).
    /// </param>
    /// <param name="interfaceName">
    ///   Interface name without namespace.
    ///   If not specified, will search for all interfaces in the assembly.
    /// </param>
    /// <param name="version">
    ///   (Optional) Version of the package. If not specified, the latest version will be used.
    /// </param>    [McpServerTool]
    [Description(
       "Extracts and returns the C# interface definition from a specified NuGet package. " +
       "Parameters: " +
       "packageId — NuGet package ID; " +
       "version (optional) — package version (defaults to latest); " +
       "interfaceName (optional) — short interface name without namespace."
    )]
    public Task<string> GetInterfaceDefinition(
        string packageId,
        string interfaceName,
        string? version = null)
    {
        return ExecuteWithLoggingAsync(
            () => GetInterfaceDefinitionCore(packageId, interfaceName, version),
            Logger,
            "Error fetching interface definition");
    }

    private async Task<string> GetInterfaceDefinitionCore(
        string packageId,
        string interfaceName,
        string? version)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentNullException(nameof(interfaceName));

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Fetching interface {InterfaceName} from package {PackageId} version {Version}",
            interfaceName, packageId, version);

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        // Search in each DLL in the archive
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            var definition = await TryGetInterfaceFromEntry(entry, interfaceName);
            if (definition != null)
                return definition;
        }

        return $"Interface '{interfaceName}' not found in package {packageId}.";
    }

    private async Task<string?> TryGetInterfaceFromEntry(ZipArchiveEntry entry, string interfaceName)
    {
        try
        {
            // Read the DLL into memory
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);

            var assemblyData = ms.ToArray();
            var assembly = PackageService.LoadAssemblyFromMemory(assemblyData);

            if (assembly == null) return null;

            var iface = assembly.GetTypes()
                .FirstOrDefault(t =>
                {
                    if (!t.IsInterface) return false;

                    // Exact match
                    if (t.Name == interfaceName) return true;

                    // For generic types, compare the name part before the backtick
                    if (!t.IsGenericType) return false;
                    {
                        var backtickIndex = t.Name.IndexOf('`');
                        if (backtickIndex > 0)
                        {
                            var baseName = t.Name.Substring(0, backtickIndex);
                            return baseName == interfaceName;
                        }
                    }

                    return false;
                });

            if (iface == null)
                return null;

            return _formattingService.FormatInterfaceDefinition(iface, Path.GetFileName(entry.FullName));
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
            return null;
        }
    }
}
