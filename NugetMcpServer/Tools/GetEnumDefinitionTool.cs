using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Server;

using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Models;
using NuGetMcpServer.Services;

using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class GetEnumDefinitionTool(
    ILogger<GetEnumDefinitionTool> logger,
    NuGetPackageService packageService,
    EnumFormattingService formattingService) : McpToolBase<GetEnumDefinitionTool>(logger, packageService)
{    [McpServerTool]
    [Description("Extracts and returns the C# enum definition from a specified NuGet package.")]
    public Task<string> GetEnumDefinition(
        [Description("NuGet package ID")] string packageId,
        [Description("Enum name (short name like 'DayOfWeek' or full name like 'System.DayOfWeek')")] string enumName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        return ExecuteWithLoggingAsync(
            () => GetEnumDefinitionCore(packageId, enumName, version, progress),
            Logger,
            "Error fetching enum definition");
    }    private async Task<string> GetEnumDefinitionCore(
        string packageId,
        string enumName,
        string? version,
        IProgress<ProgressNotificationValue>? progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(enumName))
        {
            throw new ArgumentNullException(nameof(enumName));
        }

        progress?.Report(new ProgressNotificationValue(10, "Resolving package version", 1, 4));

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Fetching enum {EnumName} from package {PackageId} version {Version}", enumName, packageId, version);

        progress?.Report(new ProgressNotificationValue(30, "Downloading package", 2, 4, $"{packageId} v{version}"));

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress?.Report(new ProgressNotificationValue(70, "Scanning assemblies for enum", 3, 4));

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var definition = await TryGetEnumFromEntry(entry, enumName);
            if (definition != null)
            {
                progress?.Report(new ProgressNotificationValue(100, "Enum found", 4, 4, enumName));
                return definition;
            }
        }

        return $"Enum '{enumName}' not found in package {packageId}.";
    }

    private async Task<string?> TryGetEnumFromEntry(ZipArchiveEntry entry, string enumName)
    {
        try
        {
            var assembly = await LoadAssemblyFromEntryAsync(entry);

            if (assembly == null)
            {
                return null;
            }
            var enumType = assembly.GetTypes()
                .FirstOrDefault(t => t.IsEnum && (t.Name == enumName || t.FullName == enumName));

            if (enumType == null)
            {
                return null;
            }

            var assemblyName = Path.GetFileName(entry.FullName);
            return $"/* C# ENUM FROM {assemblyName} */\r\n" + formattingService.FormatEnumDefinition(enumType);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
            return null;
        }
    }
}
