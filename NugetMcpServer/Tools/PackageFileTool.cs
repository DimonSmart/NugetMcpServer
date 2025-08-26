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
public class PackageFileTool(ILogger<PackageFileTool> logger, NuGetPackageService packageService) : McpToolBase<PackageFileTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Lists all files inside a NuGet package.")]
    public Task<FileListResult> list_package_files(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => ListFilesCore(packageId, version, progressNotifier),
            Logger,
            "Error listing package files");
    }

    [McpServerTool]
    [Description("Reads a file from a NuGet package. Returns text or base64 for binary files. Supports optional offset and byte count. Max chunk size: 1 MB.")]
    public Task<FileContentResult> get_package_file(
        [Description("NuGet package ID")] string packageId,
        [Description("File path inside the package")] string filePath,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Byte offset within the file (optional)")] long offset = 0,
        [Description("Maximum number of bytes to read (optional, max: 1048576)")] int? bytes = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetFileCore(packageId, filePath, version, offset, bytes, progressNotifier),
            Logger,
            "Error reading package file");
    }

    private async Task<FileListResult> ListFilesCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        progress.ReportMessage("Resolving package version");
        if (version.IsNullOrEmptyOrNullString())
            version = await PackageService.GetLatestVersion(packageId);

        progress.ReportMessage($"Downloading package {packageId} v{version}");
        var files = await PackageService.ListPackageFilesAsync(packageId, version!, progress);

        return new FileListResult
        {
            PackageId = packageId,
            Version = version!,
            Files = files.ToList()
        };
    }

    private async Task<FileContentResult> GetFileCore(string packageId, string filePath, string? version, long offset, int? bytes, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        progress.ReportMessage("Resolving package version");
        if (version.IsNullOrEmptyOrNullString())
            version = await PackageService.GetLatestVersion(packageId);

        progress.ReportMessage($"Downloading package {packageId} v{version}");
        var result = await PackageService.GetPackageFileAsync(packageId, version!, filePath, offset, bytes, progress);
        return result;
    }
}

