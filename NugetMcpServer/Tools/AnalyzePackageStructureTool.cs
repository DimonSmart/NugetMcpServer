using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;

using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class AnalyzePackageStructureTool(
    ILogger<AnalyzePackageStructureTool> logger,
    NuGetPackageService packageService) : McpToolBase<AnalyzePackageStructureTool>(logger, packageService)
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

        var analysis = await AnalyzePackageAsync(packageStream, packageId, version);

        progress.ReportMessage("Package analysis completed");

        return FormatAnalysisResult(analysis);
    }

    private async Task<PackageAnalysisResult> AnalyzePackageAsync(Stream packageStream, string packageId, string version)
    {
        var result = new PackageAnalysisResult
        {
            PackageId = packageId,
            Version = version
        };

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        // 1. Analyze .nuspec file
        var nuspecEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry != null)
        {
            await AnalyzeNuspecAsync(nuspecEntry, result);
        }

        // 2. Analyze lib/ folder content
        AnalyzeLibContent(archive, result);

        // 3. Determine if it's a meta-package
        result.IsMetaPackage = DetermineIfMetaPackage(result);

        return result;
    }

    private async Task AnalyzeNuspecAsync(ZipArchiveEntry nuspecEntry, PackageAnalysisResult result)
    {
        using var nuspecStream = nuspecEntry.Open();
        using var reader = new StreamReader(nuspecStream);
        var nuspecContent = await reader.ReadToEndAsync();

        var doc = XDocument.Parse(nuspecContent);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        var metadata = doc.Root?.Element(ns + "metadata");
        result.Description = metadata?.Element(ns + "description")?.Value ?? string.Empty;

        var dependencyGroups = doc.Root?.Descendants(ns + "dependencies")?.Elements(ns + "group") ?? [];
        var allDependencies = doc.Root?.Descendants(ns + "dependency") ?? [];

        foreach (var dep in allDependencies)
        {
            var id = dep.Attribute("id")?.Value ?? string.Empty;
            var versionAttr = dep.Attribute("version")?.Value ?? string.Empty;
            var targetFramework = dep.Parent?.Attribute("targetFramework")?.Value ?? "Any";

            if (!string.IsNullOrEmpty(id))
            {
                result.Dependencies.Add(new PackageDependency
                {
                    Id = id,
                    Version = versionAttr,
                    TargetFramework = targetFramework
                });
            }
        }
    }

    private static void AnalyzeLibContent(ZipArchive archive, PackageAnalysisResult result)
    {
        var libFiles = archive.Entries
            .Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            .Where(e => !e.FullName.EndsWith("/"))
            .ToList();

        result.LibFiles = libFiles.Select(f => f.FullName).ToList();
    }

    private bool DetermineIfMetaPackage(PackageAnalysisResult analysis)
    {
        // Method 1: No lib files but has dependencies
        if (!analysis.LibFiles.Any() && analysis.Dependencies.Any())
        {
            Logger.LogDebug("Package {PackageId} determined as meta-package: no lib files but has dependencies", analysis.PackageId);
            return true;
        }

        // Method 2: Only placeholder/reference assemblies (very small DLLs with no real types)
        var dllFiles = analysis.LibFiles.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
        if (dllFiles.Any() && analysis.Dependencies.Count >= 2)
        {
            Logger.LogDebug("Package {PackageId} might be meta-package: has {DllCount} DLLs and {DepCount} dependencies",
                analysis.PackageId, dllFiles.Count, analysis.Dependencies.Count);
        }

        // Method 3: Check if description suggests it's a meta-package
        if (analysis.Description.Contains("meta", StringComparison.OrdinalIgnoreCase) ||
            analysis.Description.Contains("umbrella", StringComparison.OrdinalIgnoreCase) ||
            analysis.Description.Contains("collection", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogDebug("Package {PackageId} determined as meta-package: description suggests meta-package", analysis.PackageId);
            return true;
        }

        // Method 4: High dependency to content ratio
        if (analysis.Dependencies.Count >= 3 && analysis.LibFiles.Count <= 2)
        {
            Logger.LogDebug("Package {PackageId} determined as meta-package: high dependency to content ratio", analysis.PackageId);
            return true;
        }

        return false;
    }

    private string FormatAnalysisResult(PackageAnalysisResult analysis)
    {
        if (analysis.IsMetaPackage)
        {
            return FormatMetaPackageResult(analysis);
        }
        else
        {
            return FormatRegularPackageResult(analysis);
        }
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
