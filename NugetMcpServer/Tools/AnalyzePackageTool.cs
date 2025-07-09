using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
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
public class AnalyzePackageTool(ILogger<AnalyzePackageTool> logger, NuGetPackageService packageService) : McpToolBase<AnalyzePackageTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Analyzes a NuGet package and returns either class information or meta-package information.")]
    public Task<string> AnalyzePackage(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => AnalyzePackageCore(packageId, version, progressNotifier),
            Logger,
            "Error analyzing package");
    }

    private async Task<string> AnalyzePackageCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        // Ensure we have non-null values for packageId and version
        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Analyzing package {PackageId} version {Version}", packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        // First, check if this is a meta-package
        progress.ReportMessage("Checking package structure");
        var isMetaPackage = await PackageService.IsMetaPackageAsync(packageStream);
        
        if (isMetaPackage)
        {
            packageStream.Position = 0;
            var dependencies = PackageService.GetPackageDependencies(packageStream);
            var description = PackageService.GetPackageDescription(packageStream);
            
            var metaResult = new MetaPackageResult
            {
                PackageId = packageId,
                Version = version,
                Dependencies = dependencies,
                Description = description
            };
            
            progress.ReportMessage($"Meta-package detected with {dependencies.Count} dependencies");
            
            return metaResult.ToFormattedString();
        }

        progress.ReportMessage("Scanning assemblies for classes");

        // Reset stream position
        packageStream.Position = 0;
        
        var classResult = new ClassListResult
        {
            PackageId = packageId,
            Version = version
        };
        
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        var dllEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
        
        foreach (var entry in dllEntries)
        {
            ProcessArchiveEntry(entry, classResult);
        }

        progress.ReportMessage($"Class listing completed - Found {classResult.Classes.Count} classes");
        
        return classResult.ToFormattedString();
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
