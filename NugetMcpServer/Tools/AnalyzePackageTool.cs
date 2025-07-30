using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
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
    public Task<string> analyze_package(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => AnalyzePackageCore(packageId, version, progressNotifier),
            Logger,
            "Error analyzing package");
    }

    private async Task<string> AnalyzePackageCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        (LoadedPackageAssemblies loaded, PackageInfo packageInfo, string resolvedVersion) =
            await _archiveProcessingService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation("Analyzing package {PackageId} version {Version}", packageId, resolvedVersion);

        if (packageInfo.IsMetaPackage)
        {
            var metaResult = new MetaPackageResult
            {
                PackageId = packageId,
                Version = resolvedVersion,
                Dependencies = packageInfo.Dependencies,
                Description = packageInfo.Description ?? string.Empty
            };

            progress.ReportMessage($"Meta-package detected with {packageInfo.Dependencies.Count} dependencies");

            return metaResult.Format();
        }

        progress.ReportMessage("Scanning assemblies for classes");

        ClassListResult classResult = new ClassListResult
        {
            PackageId = packageId,
            Version = resolvedVersion
        };

        foreach (LoadedAssemblyInfo assemblyInfo in loaded.Assemblies)
        {
            System.Collections.Generic.List<Type> classes = assemblyInfo.Types
                .Where(t => t.IsClass && (t.IsPublic || t.IsNestedPublic))
                .ToList();

            foreach (Type? cls in classes)
            {
                classResult.Classes.Add(new ClassInfo
                {
                    Name = cls.Name,
                    FullName = cls.FullName ?? string.Empty,
                    AssemblyName = assemblyInfo.FileName,
                    IsStatic = cls.IsAbstract && cls.IsSealed,
                    IsAbstract = cls.IsAbstract && !cls.IsSealed,
                    IsSealed = cls.IsSealed && !cls.IsAbstract
                });
            }
        }

        progress.ReportMessage($"Class listing completed - Found {classResult.Classes.Count} classes");

        return classResult.Format();
    }

}
