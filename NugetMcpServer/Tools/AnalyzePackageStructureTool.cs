using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using NuGet.Packaging;

using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;

using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class AnalyzePackageStructureTool(
    ILogger<AnalyzePackageStructureTool> logger,
    NuGetPackageService packageService,
    MetaPackageDetector metaPackageDetector) : McpToolBase<AnalyzePackageStructureTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Analyzes NuGet package structure to determine if it's a meta-package and lists its dependencies.")]
    public Task<string> AnalyzePackageStructure(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => AnalyzePackageStructureCore(packageId, version, progressNotifier),
            Logger,
            "Error analyzing package structure");
    }

    private async Task<string> AnalyzePackageStructureCore(
        string packageId,
        string? version,
        ProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        packageId = packageId ?? string.Empty;
        version = version ?? string.Empty;

        Logger.LogInformation("Analyzing package structure for {PackageId} version {Version}",
            packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress.ReportMessage("Analyzing package structure");

        var analysis = AnalyzePackage(packageStream, packageId, version);

        progress.ReportMessage("Package analysis completed");

        return FormatAnalysisResult(analysis);
    }

    private PackageAnalysisResult AnalyzePackage(Stream packageStream, string packageId, string version)
    {
        var result = new PackageAnalysisResult
        {
            PackageId = packageId,
            Version = version
        };

        packageStream.Position = 0;
        using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        
        // 1. Analyze .nuspec file
        AnalyzeNuspec(reader, result);

        // 2. Analyze lib/ folder content
        AnalyzeLibContent(reader, result);

        // 3. Determine if it's a meta-package
        packageStream.Position = 0;
        result.IsMetaPackage = metaPackageDetector.IsMetaPackage(packageStream, packageId);

        return result;
    }

    private void AnalyzeNuspec(PackageArchiveReader reader, PackageAnalysisResult result)
    {
        using var nuspecStream = reader.GetNuspec();
        var nuspecReader = new NuspecReader(nuspecStream);
        
        result.Description = nuspecReader.GetDescription() ?? string.Empty;

        // Check for explicit packageType = "Dependency"
        var packageTypes = nuspecReader.GetPackageTypes();
        result.HasDependencyPackageType = packageTypes.Any(pt => 
            string.Equals(pt.Name, "Dependency", StringComparison.OrdinalIgnoreCase));

        var dependencyGroups = nuspecReader.GetDependencyGroups();
        foreach (var group in dependencyGroups)
        {
            foreach (var dep in group.Packages)
            {
                result.Dependencies.Add(new PackageDependency
                {
                    Id = dep.Id,
                    Version = dep.VersionRange?.ToString() ?? string.Empty,
                    TargetFramework = group.TargetFramework?.GetShortFolderName() ?? "Any"
                });
            }
        }
    }

    private static void AnalyzeLibContent(PackageArchiveReader reader, PackageAnalysisResult result)
    {
        var files = reader.GetFiles();
        var libFiles = files
            .Where(f => f.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("/"))
            .Where(f => !f.EndsWith("/_._", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("\\_._", StringComparison.OrdinalIgnoreCase))
            .ToList();

        result.LibFiles = libFiles;
    }

    private string FormatAnalysisResult(PackageAnalysisResult analysis)
    {
        if (analysis.IsMetaPackage)
            return FormatMetaPackageResult(analysis);
            
        return FormatRegularPackageResult(analysis);
    }

    private string FormatMetaPackageResult(PackageAnalysisResult analysis)
    {
        var result = $"PACKAGE_TYPE: META_PACKAGE\n";
        result += $"PACKAGE_ID: {analysis.PackageId}\n";
        result += $"VERSION: {analysis.Version}\n";
        result += $"DESCRIPTION: {analysis.Description}\n\n";

        result += "DEPENDENCIES:\n";
        var uniqueDeps = analysis.Dependencies
            .GroupBy(d => d.Id)
            .Select(g => g.First())
            .OrderBy(d => d.Id)
            .ToList();

        foreach (var dep in uniqueDeps)
        {
            result += $"  {dep.Id}|{dep.Version}|{dep.TargetFramework}\n";
        }

        result += "\nRECOMMENDATION: Analyze the dependencies listed above to find actual implementations.\n";

        return result;
    }

    private string FormatRegularPackageResult(PackageAnalysisResult analysis)
    {
        var result = $"PACKAGE_TYPE: REGULAR_PACKAGE\n";
        result += $"PACKAGE_ID: {analysis.PackageId}\n";
        result += $"VERSION: {analysis.Version}\n";
        result += $"DESCRIPTION: {analysis.Description}\n\n";

        result += $"LIB_FILES_COUNT: {analysis.LibFiles.Count}\n";
        if (analysis.LibFiles.Any())
        {
            result += "LIB_FILES:\n";
            foreach (var file in analysis.LibFiles.Take(10))
            {
                result += $"  {file}\n";
            }
        }

        if (analysis.Dependencies.Any())
        {
            result += $"\nDEPENDENCIES_COUNT: {analysis.Dependencies.Count}\n";
        }

        result += "\nRECOMMENDATION: This package contains actual implementations. Use class/interface listing tools.\n";

        return result;
    }
}
