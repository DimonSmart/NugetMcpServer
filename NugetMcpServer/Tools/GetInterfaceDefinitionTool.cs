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
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetInterfaceDefinitionCore(packageId, interfaceName, version, progressNotifier),
            Logger,
            "Error fetching interface definition");
    }

    private async Task<string> GetInterfaceDefinitionCore(
        string packageId,
        string interfaceName,
        string? version,
        ProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new ArgumentNullException(nameof(interfaceName));
        }

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Fetching interface {InterfaceName} from package {PackageId} version {Version}",
            interfaceName, packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version);

        var metaPackageWarning = string.Empty;
        if (packageInfo.IsMetaPackage)
        {
            metaPackageWarning = $"⚠️  META-PACKAGE: {packageId} v{version}\n";
            metaPackageWarning += "This package groups other related packages together and may not contain actual implementation code.\n";

            if (packageInfo.Dependencies.Count > 0)
            {
                metaPackageWarning += "Dependencies:\n";
                foreach (var dependency in packageInfo.Dependencies)
                {
                    metaPackageWarning += $"  • {dependency.Id} ({dependency.Version})\n";
                }
                metaPackageWarning += "💡 To see actual implementations, analyze one of the dependency packages listed above.\n";
            }
            metaPackageWarning += "\n" + new string('-', 60) + "\n\n";
        }

        progress.ReportMessage("Scanning assemblies for interface");

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        var dllEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();

        // Filter to avoid duplicate DLLs from different target frameworks
        var uniqueDllEntries = FilterUniqueAssemblies(dllEntries);
        var processedDlls = 0;

        foreach (var entry in uniqueDllEntries)
        {
            progress.ReportMessage($"Scanning {Path.GetFileName(entry.FullName)}: {entry.FullName}");

            var definition = await TryGetInterfaceFromEntry(entry, interfaceName);
            if (definition != null)
            {
                progress.ReportMessage($"Interface found: {interfaceName}");
                return metaPackageWarning + definition;
            }
            processedDlls++;
        }

        return metaPackageWarning + $"Interface '{interfaceName}' not found in package {packageId}.";
    }
    private async Task<string?> TryGetInterfaceFromEntry(ZipArchiveEntry entry, string interfaceName)
    {
        try
        {
            var (assembly, types) = await LoadAssemblyFromEntryWithTypesAsync(entry);

            if (assembly == null) return null;

            var iface = types
                .FirstOrDefault(t =>
                {
                    if (!t.IsInterface)
                    {
                        return false;
                    }

                    if (t.Name == interfaceName)
                    {
                        return true;
                    }

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
