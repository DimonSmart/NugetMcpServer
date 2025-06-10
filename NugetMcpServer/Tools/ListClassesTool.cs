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
public class ListClassesTool(ILogger<ListClassesTool> logger, NuGetPackageService packageService) : McpToolBase<ListClassesTool>(logger, packageService)
{    [McpServerTool]
    [Description("Lists all public classes available in a specified NuGet package.")]
    public Task<ClassListResult> ListClasses(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        return ExecuteWithLoggingAsync(
            () => ListClassesCore(packageId, version, progress),
            Logger,
            "Error listing classes");
    }    private async Task<ClassListResult> ListClassesCore(string packageId, string? version, IProgress<ProgressNotificationValue>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        progress?.Report(new ProgressNotificationValue(10, "Resolving package version", 1, 4));

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        // Ensure we have non-null values for packageId and version
        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Listing classes from package {PackageId} version {Version}",
            packageId, version);

        progress?.Report(new ProgressNotificationValue(30, "Downloading package", 2, 4, $"{packageId} v{version}"));

        var result = new ClassListResult
        {
            PackageId = packageId,
            Version = version,
            Classes = []
        };

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress?.Report(new ProgressNotificationValue(70, "Scanning assemblies for classes", 3, 4));

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            ProcessArchiveEntry(entry, result);
        }

        progress?.Report(new ProgressNotificationValue(100, "Class listing completed", 4, 4, $"Found {result.Classes.Count} classes"));

        return result;
    }

    private void ProcessArchiveEntry(ZipArchiveEntry entry, ClassListResult result)
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
            var classes = assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && !t.IsNested) // Public classes, excluding nested classes
                .ToList();

            foreach (var cls in classes)
            {
                result.Classes.Add(new ClassInfo
                {
                    Name = cls.Name,
                    FullName = cls.FullName ?? string.Empty,
                    AssemblyName = assemblyName,
                    IsStatic = cls.IsAbstract && cls.IsSealed,
                    IsAbstract = cls.IsAbstract && !cls.IsSealed,
                    IsSealed = cls.IsSealed && !cls.IsAbstract
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error processing archive entry {EntryName}", entry.FullName);
        }
    }
}
