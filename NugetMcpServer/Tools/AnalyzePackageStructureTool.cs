using System;
using System.Collections.Generic;
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
using NuGetMcpServer.Models;
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
    public Task<string> analyze_package_structure(
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

        Logger.LogInformation("Analyzing package structure for {PackageId} version {Version}",
            packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Analyzing package structure");

        var analysis = AnalyzePackage(packageStream);
        analysis.PackageId = packageId;
        analysis.Version = version!;

        progress.ReportMessage("Package analysis completed");

        return FormatAnalysisResult(analysis);
    }

    private PackageAnalysisResult AnalyzePackage(Stream packageStream)
    {
        packageStream.Position = 0;
        using var reader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);

        var nuspecData = AnalyzeNuspec(reader);
        var libFiles = AnalyzeLibContent(reader);

        packageStream.Position = 0;
        var isMetaPackage = metaPackageDetector.IsMetaPackage(packageStream);

        return new PackageAnalysisResult
        {
            Description = nuspecData.Description,
            HasDependencyPackageType = nuspecData.HasDependencyPackageType,
            Dependencies = nuspecData.Dependencies,
            LibFiles = libFiles,
            IsMetaPackage = isMetaPackage
        };
    }

    private (string Description, bool HasDependencyPackageType, List<PackageDependency> Dependencies) AnalyzeNuspec(PackageArchiveReader reader)
    {
        using var nuspecStream = reader.GetNuspec();
        var nuspecReader = new NuspecReader(nuspecStream);

        var description = nuspecReader.GetDescription() ?? string.Empty;
        var hasDependencyPackageType = HasDependencyPackageType(nuspecReader);
        var dependencies = ExtractDependencies(nuspecReader);

        return (description, hasDependencyPackageType, dependencies);
    }

    private static bool HasDependencyPackageType(NuspecReader nuspecReader)
    {
        var packageTypes = nuspecReader.GetPackageTypes();
        return packageTypes.Any(pt => string.Equals(pt.Name, "Dependency", StringComparison.OrdinalIgnoreCase));
    }

    private static List<PackageDependency> ExtractDependencies(NuspecReader nuspecReader)
    {
        var dependencies = new List<PackageDependency>();
        var dependencyGroups = nuspecReader.GetDependencyGroups();

        foreach (var group in dependencyGroups)
        {
            foreach (var dep in group.Packages)
            {
                dependencies.Add(new PackageDependency
                {
                    Id = dep.Id,
                    Version = dep.VersionRange?.ToString() ?? string.Empty,
                    TargetFramework = group.TargetFramework?.GetShortFolderName() ?? "Any"
                });
            }
        }

        return dependencies;
    }

    private static List<string> AnalyzeLibContent(PackageArchiveReader reader)
    {
        var files = reader.GetFiles();
        return files
            .Where(f => f.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("/"))
            .Where(f => !f.EndsWith("/_._", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("\\_._", StringComparison.OrdinalIgnoreCase))
            .ToList();
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
            result += "DEPENDENCIES:\n";

            var uniqueDeps = analysis.Dependencies
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderBy(d => d.Id)
                .Take(100)
                .ToList();

            foreach (var dep in uniqueDeps)
            {
                result += $"  - {dep.Id} ({dep.Version})\n";
            }

            if (analysis.Dependencies.Count > 100)
            {
                result += $"  ... and {analysis.Dependencies.Count - 100} more\n";
            }
        }

        result += $"\nRECOMMENDATION: {GenerateSmartRecommendations(analysis)}\n";

        return result;
    }

    private string GenerateSmartRecommendations(PackageAnalysisResult analysis)
    {
        var recommendations = new List<string>();

        recommendations.Add("This package contains actual implementations. Use class/interface listing tools.");

        if (!analysis.Dependencies.Any())
        {
            return string.Join(" ", recommendations);
        }

        var packageNameParts = analysis.PackageId.Split('.');
        if (packageNameParts.Length >= 2)
        {
            var packagePrefix = string.Join(".", packageNameParts.Take(2));

            var relatedDependencies = analysis.Dependencies
                .Where(d => d.Id.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(d.Id, analysis.PackageId, StringComparison.OrdinalIgnoreCase))
                .Select(d => d.Id)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (relatedDependencies.Any())
            {
                recommendations.Add($"Related packages in the same family that may contain additional implementations: {string.Join(", ", relatedDependencies)}.");
            }
        }

        if (analysis.Dependencies.Count > 0)
        {
            recommendations.Add("Consider exploring dependencies for additional functionality.");
        }

        return string.Join(" ", recommendations);
    }
}
