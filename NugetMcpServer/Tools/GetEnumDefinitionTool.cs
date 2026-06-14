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
public class GetEnumDefinitionTool(
    ILogger<GetEnumDefinitionTool> logger,
    NuGetPackageService packageService,
    ApiDefinitionFormatter formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetEnumDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# enum definition from a specified NuGet package.")]
    public Task<string> get_enum_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Enum name (short name like 'DayOfWeek' or full name like 'System.DayOfWeek')")] string enumName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null,
        [Description("Optional NuGet source name or URL/path")] string? source = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetEnumDefinitionCore(packageId, enumName, version, progressNotifier, source),
            Logger,
            "Error fetching enum definition");
    }
    private async Task<string> GetEnumDefinitionCore(
        string packageId,
        string enumName,
        string? version,
        ProgressNotifier progress,
        string? source)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(enumName))
        {
            throw new ArgumentNullException(nameof(enumName));
        }

        progress.ReportMessage("Resolving package version");

        LoadedPackageMetadata loaded =
            await archiveService.LoadPackageMetadataAsync(packageId, version, progress, source);

        Logger.LogInformation(
            "Fetching enum {EnumName} from package {PackageId} version {Version}",
            enumName, packageId, loaded.Version);

        string metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(loaded.PackageInfo);

        progress.ReportMessage("Scanning assemblies for enum");

        foreach (ApiAssemblyModel assemblyInfo in loaded.Api.Assemblies)
        {
            progress.ReportMessage($"Scanning {assemblyInfo.FileName}: {assemblyInfo.PackagePath}");
            var enumType = assemblyInfo.Types.FirstOrDefault(t =>
                t.Kind == ApiTypeKind.Enum && ApiModelSearch.Matches(t, enumName));
            if (enumType != null)
            {
                progress.ReportMessage($"Enum found: {enumName}");
                return metaPackageWarning + formattingService.FormatTypeDefinition(enumType, packageId, loaded.Version, assemblyInfo.TargetFramework);
            }
        }

        return metaPackageWarning + $"Enum '{enumName}' not found in package {packageId}.";
    }
}
