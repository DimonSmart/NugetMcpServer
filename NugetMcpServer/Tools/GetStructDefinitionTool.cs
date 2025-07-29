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
public class GetStructDefinitionTool(
    ILogger<GetStructDefinitionTool> logger,
    NuGetPackageService packageService,
    ClassFormattingService formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetStructDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# struct definition from a specified NuGet package.")]
    public Task<string> get_struct_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Struct name (short or full name)")] string structName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetStructDefinitionCore(packageId, structName, version, progressNotifier),
            Logger,
            "Error fetching struct definition");
    }

    private async Task<string> GetStructDefinitionCore(
        string packageId,
        string structName,
        string? version,
        ProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        if (string.IsNullOrWhiteSpace(structName))
            throw new ArgumentNullException(nameof(structName));

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
            version = await PackageService.GetLatestVersion(packageId);

        Logger.LogInformation("Fetching struct {StructName} from package {PackageId} version {Version}", structName, packageId, version!);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);

        var metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(packageInfo, packageId, version!);

        progress.ReportMessage("Scanning assemblies for struct");

        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        var dllFiles = ArchiveProcessingService.GetUniqueAssemblyFiles(packageReader);

        foreach (var filePath in dllFiles)
        {
            var assemblyInfo = await archiveService.LoadAssemblyFromPackageFileAsync(packageReader, filePath);
            if (assemblyInfo != null)
            {
                try
                {
                    var structType = assemblyInfo.Types.FirstOrDefault(t => t.IsValueType && !t.IsEnum && (t.Name == structName || t.FullName == structName || GenericMatch(t, structName)));
                    if (structType != null)
                    {
                        progress.ReportMessage($"Struct found: {structName}");
                        var formatted = formattingService.FormatClassDefinition(structType, assemblyInfo.AssemblyName, packageId, assemblyInfo.AssemblyBytes);
                        return metaPackageWarning + formatted;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Error processing assembly {AssemblyName}", assemblyInfo.AssemblyName);
                }
            }
        }

        return metaPackageWarning + $"Struct '{structName}' not found in package {packageId}.";
    }

    private static bool GenericMatch(Type type, string name)
    {
        if (!type.IsGenericType)
            return false;

        var backtickIndex = type.Name.IndexOf('`');
        if (backtickIndex > 0 && type.Name[..backtickIndex] == name)
            return true;

        if (type.FullName is { } fullName)
        {
            var fullBacktickIndex = fullName.IndexOf('`');
            if (fullBacktickIndex > 0 && fullName[..fullBacktickIndex] == name)
                return true;
        }
        return false;
    }
}
