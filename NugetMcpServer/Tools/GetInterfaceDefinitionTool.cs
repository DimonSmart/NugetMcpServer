using System;
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
public class GetInterfaceDefinitionTool(
    ILogger<GetInterfaceDefinitionTool> logger,
    NuGetPackageService packageService,
    InterfaceFormattingService formattingService) : McpToolBase<GetInterfaceDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# interface definition from a specified NuGet package.")]
    public Task<string> GetInterfaceDefinition(
        [Description("NuGet package ID")] string packageId,
        [Description("Interface name (short name like 'IDisposable' or full name like 'System.IDisposable')")] string interfaceName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        return ExecuteWithLoggingAsync(
            () => GetInterfaceDefinitionCore(packageId, interfaceName, version, progress),
            Logger,
            "Error fetching interface definition");
    }
    private async Task<string> GetInterfaceDefinitionCore(
        string packageId,
        string interfaceName,
        string? version,
        IProgress<ProgressNotificationValue>? progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new ArgumentNullException(nameof(interfaceName));
        }

        progress?.Report(new ProgressNotificationValue() { Progress = 10, Total = 100, Message = "Resolving package version" });

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Fetching interface {InterfaceName} from package {PackageId} version {Version}",
            interfaceName, packageId, version);

        progress?.Report(new ProgressNotificationValue() { Progress = 30, Total = 100, Message = $"Downloading package {packageId} v{version}" });

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress?.Report(new ProgressNotificationValue() { Progress = 70, Total = 100, Message = "Scanning assemblies for interface" });

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        var dllEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
        var processedDlls = 0;

        foreach (var entry in dllEntries)
        {
            progress?.Report(new ProgressNotificationValue() { Progress = (float)(70 + (processedDlls * 25.0 / dllEntries.Count)), Total = 100, Message = $"Scanning {Path.GetFileName(entry.FullName)}: {entry.FullName}" });

            var definition = await TryGetInterfaceFromEntry(entry, interfaceName);
            if (definition != null)
            {
                progress?.Report(new ProgressNotificationValue() { Progress = 100, Total = 100, Message = $"Interface found: {interfaceName}" });
                return definition;
            }
            processedDlls++;
        }

        return $"Interface '{interfaceName}' not found in package {packageId}.";
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
                .FirstOrDefault(t =>
                {
                    if (!t.IsInterface)
                    {
                        return false;
                    }

                    // Exact match for short name
                    if (t.Name == interfaceName)
                    {
                        return true;
                    }

                    // Exact match for full name
                    if (t.FullName == interfaceName)
                    {
                        return true;
                    }

                    // For generic types, compare the name part before the backtick
                    if (!t.IsGenericType)
                    {
                        return false;
                    }

                    {
                        var backtickIndex = t.Name.IndexOf('`');
                        if (backtickIndex > 0)
                        {
                            var baseName = t.Name.Substring(0, backtickIndex);
                            if (baseName == interfaceName)
                            {
                                return true;
                            }
                        }

                        // Also check full name for generics
                        if (t.FullName != null)
                        {
                            var fullBacktickIndex = t.FullName.IndexOf('`');
                            if (fullBacktickIndex > 0)
                            {
                                var fullBaseName = t.FullName.Substring(0, fullBacktickIndex);
                                return fullBaseName == interfaceName;
                            }
                        }
                    }

                    return false;
                });

            if (iface == null)
            {
                return null;
            }

            return formattingService.FormatInterfaceDefinition(iface, Path.GetFileName(entry.FullName));
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
            return null;
        }
    }
}
