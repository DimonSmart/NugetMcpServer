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
public class ListRecordsTool(ILogger<ListRecordsTool> logger, NuGetPackageService packageService, ArchiveProcessingService archiveProcessingService) : McpToolBase<ListRecordsTool>(logger, packageService)
{
    private readonly ArchiveProcessingService _archiveProcessingService = archiveProcessingService;

    [McpServerTool]
    [Description("Lists all public records available in a specified NuGet package.")]
    public Task<RecordListResult> list_records(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => ListRecordsCore(packageId, version, progressNotifier),
            Logger,
            "Error listing records");
    }

    private async Task<RecordListResult> ListRecordsCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        var (loaded, packageInfo, resolvedVersion) =
            await _archiveProcessingService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Listing records from package {PackageId} version {Version}", packageId, resolvedVersion);

        var result = new RecordListResult
        {
            PackageId = packageId,
            Version = resolvedVersion,
            Records = []
        };

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for records");

        foreach (var assemblyInfo in loaded.Assemblies)
        {
            var records = assemblyInfo.Types
                .Where(t => TypeFormattingHelpers.IsRecordType(t) && t.IsPublic && !t.IsNested)
                .ToList();

            foreach (var rec in records)
            {
                result.Records.Add(new RecordInfo
                {
                    Name = rec.Name,
                    FullName = rec.FullName ?? string.Empty,
                    AssemblyName = assemblyInfo.FileName,
                    IsStruct = rec.IsValueType
                });
            }
        }

        progress.ReportMessage($"Record listing completed - Found {result.Records.Count} records");

        return result;
    }
}
