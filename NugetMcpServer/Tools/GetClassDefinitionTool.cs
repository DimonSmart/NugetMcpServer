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
public class GetClassDefinitionTool(
    ILogger<GetClassDefinitionTool> logger,
    NuGetPackageService packageService,
    ClassFormattingService formattingService) : McpToolBase<GetClassDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# class definition from a specified NuGet package.")]
    public Task<string> GetClassDefinition(
        [Description("NuGet package ID")] string packageId,
        [Description("Class name (short name like 'String' or full name like 'System.String')")] string className,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetClassDefinitionCore(packageId, className, version, progressNotifier),
            Logger,
            "Error fetching class definition");
    }

    private async Task<string> GetClassDefinitionCore(
        string packageId,
        string className,
        string? version,
        ProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentNullException(nameof(className));
        }

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Fetching class {ClassName} from package {PackageId} version {Version}",
            className, packageId, version);

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

        progress.ReportMessage("Scanning assemblies for class");

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var definition = await TryGetClassFromEntry(entry, className);
            if (definition != null)
            {
                progress.ReportMessage($"Class found: {className}");
                return metaPackageWarning + definition;
            }
        }

        return metaPackageWarning + $"Class '{className}' not found in package {packageId}.";
    }

    private async Task<string?> TryGetClassFromEntry(ZipArchiveEntry entry, string className)
    {
        try
        {
            var assembly = await LoadAssemblyFromEntryAsync(entry);
            if (assembly == null)
            {
                return null;
            }

            var classType = assembly.GetTypes()
                .FirstOrDefault(t =>
                {
                    if (!t.IsClass || !t.IsPublic)
                    {
                        return false;
                    }

                    // Exact match for short name
                    if (t.Name == className)
                    {
                        return true;
                    }

                    // Exact match for full name
                    if (t.FullName == className)
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
                            if (baseName == className)
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
                                return fullBaseName == className;
                            }
                        }
                    }

                    return false;
                });

            if (classType == null)
            {
                return null;
            }

            return formattingService.FormatClassDefinition(classType, Path.GetFileName(entry.FullName));
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
            return null;
        }
    }
}
