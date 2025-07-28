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

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
            version = await PackageService.GetLatestVersion(packageId);

        Logger.LogInformation("Listing records from package {PackageId} version {Version}", packageId, version!);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        var result = new RecordListResult
        {
            PackageId = packageId,
            Version = version!,
            Records = []
        };

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for records");
        packageStream.Position = 0;
        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);

        var loadedAssemblies = _archiveProcessingService.LoadAllAssembliesFromPackage(packageReader);

        foreach (var assemblyInfo in loadedAssemblies)
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
                    AssemblyName = assemblyInfo.AssemblyName,
                    IsStruct = rec.IsValueType
                });
            }
        }

        progress.ReportMessage($"Record listing completed - Found {result.Records.Count} records");

        return result;
    }
}
