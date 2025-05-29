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
        : base(logger, packageService)    {
        _formattingService = formattingService;
    }

    /// <summary>
    /// Extracts and returns the C# interface definition from a specified NuGet package.
    /// </summary>
    /// <param name="packageId">
    ///   The NuGet package ID (exactly as on nuget.org).
    /// </param>
    /// <param name="interfaceName">
    ///   Interface name (can include namespace) or short name. 
    ///   Supports both 'IMyInterface' and 'MyNamespace.IMyInterface' formats.
    ///   For generic interfaces, supports both 'IGeneric`1' and 'IGeneric' formats.
    /// </param>
    /// <param name="version">
    ///   (Optional) Version of the package. If not specified, the latest version will be used.
    /// </param>    
    [McpServerTool]
    [Description(
       "Extracts and returns the C# interface definition from a specified NuGet package. " +
       "Parameters: " +
       "packageId — NuGet package ID; " +
       "version (optional) — package version (defaults to latest); " +
       "interfaceName — interface name (supports both short names and full namespace names)."
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
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new ArgumentNullException(nameof(interfaceName));
        }

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
            {
                continue;
            }

            var definition = await TryGetInterfaceFromEntry(entry, interfaceName);
            if (definition != null)
            {
                return definition;
            }
        }        return $"Interface '{interfaceName}' not found in package {packageId}.";
    }

    private async Task<string?> TryGetInterfaceFromEntry(ZipArchiveEntry entry, string interfaceName)
    {
        try
        {
            var assembly = await LoadAssemblyFromEntryAsync(entry);
            if (assembly == null)
            {
                return null;
            }

            var iface = assembly.GetTypes()
                .FirstOrDefault(t => IsInterfaceMatch(t, interfaceName));

            if (iface == null)
            {
                return null;
            }

            return _formattingService.FormatInterfaceDefinition(iface, Path.GetFileName(entry.FullName));
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
            return null;
        }
    }

    private static bool IsInterfaceMatch(Type type, string interfaceName)
    {
        if (!type.IsInterface)
        {
            return false;
        }

        // Exact matches first
        if (type.Name == interfaceName || type.FullName == interfaceName)
        {
            return true;
        }

        // Handle generic type matching
        if (type.IsGenericType)
        {
            // Check if the provided name matches the generic base name (without `N suffix)
            if (IsGenericBaseNameMatch(type.Name, interfaceName) ||
                (type.FullName != null && IsGenericBaseNameMatch(type.FullName, interfaceName)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGenericBaseNameMatch(string typeName, string searchName)
    {
        var backtickIndex = typeName.IndexOf('`');
        if (backtickIndex <= 0)
        {
            return false;
        }

        var baseName = typeName.Substring(0, backtickIndex);
        return baseName == searchName;
    }
}
