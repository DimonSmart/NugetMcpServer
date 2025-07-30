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
using static NuGetMcpServer.Extensions.ExceptionHandlingExtensions;

namespace NuGetMcpServer.Tools;

[McpServerToolType]
public class ListClassesTool(ILogger<ListClassesTool> logger, NuGetPackageService packageService, ArchiveProcessingService archiveProcessingService) : McpToolBase<ListClassesTool>(logger, packageService)
{
    private readonly ArchiveProcessingService _archiveProcessingService = archiveProcessingService;
    [McpServerTool]
    [Description("Lists all public classes and records available in a specified NuGet package.")]
    public Task<ClassListResult> list_classes_and_records(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => ListClassesCore(packageId, version, progressNotifier),
            Logger,
            "Error listing classes and records");
    }

    private async Task<ClassListResult> ListClassesCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        (LoadedPackageAssemblies loaded, PackageInfo packageInfo, string resolvedVersion) =
            await _archiveProcessingService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Listing classes and records from package {PackageId} version {Version}",
            packageId, resolvedVersion);

        var result = new ClassListResult
        {
            PackageId = packageId,
            Version = resolvedVersion,
            Classes = []
        };

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for classes/records");

        foreach (LoadedAssemblyInfo assemblyInfo in loaded.Assemblies)
        {
            System.Collections.Generic.List<Type> classes = assemblyInfo.Types
                .Where(t => t.IsClass && (t.IsPublic || t.IsNestedPublic))
                .ToList();

            foreach (Type? cls in classes)
            {
                result.Classes.Add(new ClassInfo
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

        progress.ReportMessage($"Class listing completed - Found {result.Classes.Count} classes/records");

        return result;
    }

}
