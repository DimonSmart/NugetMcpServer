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
public class ListStructsTool(ILogger<ListStructsTool> logger, NuGetPackageService packageService, ArchiveProcessingService archiveProcessingService) : McpToolBase<ListStructsTool>(logger, packageService)
{
    private readonly ArchiveProcessingService _archiveProcessingService = archiveProcessingService;

    [McpServerTool]
    [Description("Lists all public structs available in a specified NuGet package.")]
    public Task<StructListResult> list_structs(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => ListStructsCore(packageId, version, progressNotifier),
            Logger,
            "Error listing structs");
    }

    private async Task<StructListResult> ListStructsCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        var (loaded, packageInfo, resolvedVersion) =
            await _archiveProcessingService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Listing structs from package {PackageId} version {Version}", packageId, resolvedVersion);

        var result = new StructListResult
        {
            PackageId = packageId,
            Version = resolvedVersion,
            Structs = []
        };

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for structs");

        foreach (var assemblyInfo in loaded.Assemblies)
        {
            var structs = assemblyInfo.Types
                .Where(t => t.IsValueType && !t.IsEnum && (t.IsPublic || t.IsNestedPublic))
                .ToList();

            foreach (var st in structs)
            {
                result.Structs.Add(new StructInfo
                {
                    Name = st.Name,
                    FullName = st.FullName ?? string.Empty,
                    AssemblyName = assemblyInfo.FileName
                });
            }
        }

        progress.ReportMessage($"Struct listing completed - Found {result.Structs.Count} structs");

        return result;
    }
}
