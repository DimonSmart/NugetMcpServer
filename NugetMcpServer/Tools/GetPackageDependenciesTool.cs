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
public class GetPackageDependenciesTool(
    ILogger<GetPackageDependenciesTool> logger,
    NuGetPackageService packageService) : McpToolBase<GetPackageDependenciesTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Gets dependencies of a NuGet package to help understand what other packages contain the actual implementations.")]
    public Task<string> GetPackageDependencies(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetPackageDependenciesCore(packageId, version, progressNotifier),
            Logger,
            "Error getting package dependencies");
    }

    private async Task<string> GetPackageDependenciesCore(
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

        Logger.LogInformation("Getting dependencies for package {PackageId} version {Version}",
            packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version, progress);

        progress.ReportMessage("Analyzing package dependencies");

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        
        var nuspecEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry == null)
        {
            return $"No .nuspec file found in package {packageId}.";
        }

        using var nuspecStream = nuspecEntry.Open();
        using var reader = new StreamReader(nuspecStream);
        var nuspecContent = await reader.ReadToEndAsync();
        
        var doc = XDocument.Parse(nuspecContent);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        
        var metadata = doc.Root?.Element(ns + "metadata");
        var title = metadata?.Element(ns + "title")?.Value ?? packageId;
        var description = metadata?.Element(ns + "description")?.Value ?? "No description available";
        
        var dependencies = doc.Root?.Descendants(ns + "dependency").ToList() ?? [];
        
        var result = $"/* DEPENDENCIES FOR {packageId} v{version} */\n\n";
        result += $"Title: {title}\n";
        result += $"Description: {description}\n\n";
        
        if (dependencies.Count == 0)
        {
            result += "This package has no dependencies.\n";
        }
        else
        {
            result += $"This package has {dependencies.Count} dependencies:\n\n";
            
            var uniqueDependencies = dependencies
                .Select(dep => new { 
                    Id = dep.Attribute("id")?.Value ?? "Unknown", 
                    Version = dep.Attribute("version")?.Value ?? "Unknown" 
                })
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderBy(d => d.Id)
                .ToList();
            
            foreach (var dep in uniqueDependencies)
            {
                result += $"  - {dep.Id} ({dep.Version})\n";
            }
            
            if (dependencies.Count > 0)
            {
                result += "\nTo explore the actual implementations, try listing classes/interfaces from these dependencies:\n";
                foreach (var dep in uniqueDependencies.Take(3))
                {
                    result += $"  - nuget_list_classes(packageId=\"{dep.Id}\")\n";
                }
            }
        }
        
        var dllEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
        var hasOnlySmallDlls = dllEntries.All(e => e.Length < 50000);
        
        if (dependencies.Count > 0 && hasOnlySmallDlls && dllEntries.Any())
        {
            result += "\nNOTE: This appears to be a meta-package that primarily serves to group related packages together.\n";
            result += "The actual functionality is implemented in the dependencies listed above.\n";
        }

        progress.ReportMessage("Dependencies analysis completed");

        return result;
    }
}
