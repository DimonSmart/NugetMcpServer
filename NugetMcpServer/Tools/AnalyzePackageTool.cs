using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using NuGet.Packaging;

using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;
using NuGetMcpServer.Services.Formatters;

using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class AnalyzePackageTool(ILogger<AnalyzePackageTool> logger, NuGetPackageService packageService, ArchiveProcessingService archiveProcessingService) : McpToolBase<AnalyzePackageTool>(logger, packageService)
{
    private readonly ArchiveProcessingService _archiveProcessingService = archiveProcessingService;
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

        Logger.LogInformation("Analyzing package {PackageId} version {Version}", packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);

        if (packageInfo.IsMetaPackage)
        {
            var metaResult = new MetaPackageResult
            {
                PackageId = packageId,
                Version = version!,
                Dependencies = packageInfo.Dependencies,
                Description = packageInfo.Description ?? string.Empty
            };

            progress.ReportMessage($"Meta-package detected with {packageInfo.Dependencies.Count} dependencies");

            return metaResult.Format();
        }

        progress.ReportMessage("Scanning assemblies for classes");

        packageStream.Position = 0;

        var classResult = new ClassListResult
        {
            PackageId = packageId,
            Version = version!
        };

        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        var loadedAssemblies = _archiveProcessingService.LoadAllAssembliesFromPackage(packageReader);

        foreach (var assemblyInfo in loadedAssemblies)
        {
            ProcessAssemblyTypes(assemblyInfo.Types, assemblyInfo.AssemblyName, classResult);
        }

        progress.ReportMessage($"Class listing completed - Found {classResult.Classes.Count} classes");

        return classResult.Format();
    }

    private void ProcessAssemblyTypes(Type[] types, string assemblyName, ClassListResult result)
    {
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
}
