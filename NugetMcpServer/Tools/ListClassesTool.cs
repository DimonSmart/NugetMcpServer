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
public class ListClassesTool(ILogger<ListClassesTool> logger, NuGetPackageService packageService) : McpToolBase<ListClassesTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Lists all public classes available in a specified NuGet package.")]
    public Task<ClassListResult> ListClasses(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => ListClassesCore(packageId, version, progressNotifier),
            Logger,
            "Error listing classes");
    }


    private async Task<ClassListResult> ListClassesCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        Logger.LogInformation("Listing classes from package {PackageId} version {Version}",
            packageId, version!);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        var result = new ClassListResult
        {
            PackageId = packageId,
            Version = version!,
            Classes = []
        };

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for classes");
        packageStream.Position = 0;
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        var dllEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();

        var uniqueDllEntries = FilterUniqueAssemblies(dllEntries);

        foreach (var entry in uniqueDllEntries)
        {
            ProcessArchiveEntry(entry, result);
        }

        progress.ReportMessage($"Class listing completed - Found {result.Classes.Count} classes");

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
            var (assembly, types) = PackageService.LoadAssemblyFromMemoryWithTypes(assemblyData);

            if (assembly == null) return;

            var assemblyName = Path.GetFileName(entry.FullName);
            var classes = types
                .Where(t => t.IsClass && t.IsPublic && !t.IsNested)
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
