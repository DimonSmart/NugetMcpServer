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
public class GetClassDefinitionTool(
    ILogger<GetClassDefinitionTool> logger,
    NuGetPackageService packageService,
    ApiDefinitionFormatter formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetClassDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# class, record or struct definition from a specified NuGet package.")]
    public Task<string> get_class_or_record_or_struct_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Class, record or struct name (short like 'Point' or full like 'System.Point')")] string typeName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null,
        [Description("Optional NuGet source name or URL/path")] string? source = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetClassOrRecordDefinitionCore(packageId, typeName, version, progressNotifier, source),
            Logger,
            "Error fetching class, record or struct definition");
    }

    private async Task<string> GetClassOrRecordDefinitionCore(
        string packageId,
        string typeName,
        string? version,
        ProgressNotifier progress,
        string? source)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentNullException(nameof(typeName));
        }

        progress.ReportMessage("Resolving package version");

        LoadedPackageMetadata loaded =
            await archiveService.LoadPackageMetadataAsync(packageId, version, progress, source);

        Logger.LogInformation(
            "Fetching class, record or struct {ClassName} from package {PackageId} version {Version}",
            typeName, packageId, loaded.Version);

        string metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(loaded.PackageInfo);

        progress.ReportMessage("Scanning assemblies for class/record/struct");

        foreach (ApiAssemblyModel assemblyInfo in loaded.Api.Assemblies)
        {
            progress.ReportMessage($"Scanning {assemblyInfo.FileName}: {assemblyInfo.PackagePath}");
            var classType = assemblyInfo.Types.FirstOrDefault(t => IsMatchingType(t, typeName));
            if (classType != null)
            {
                progress.ReportMessage($"Class, record or struct found: {typeName}");
                string formatted = formattingService.FormatTypeDefinition(classType, packageId, loaded.Version, assemblyInfo.TargetFramework);
                return metaPackageWarning + formatted;
            }
        }

        return metaPackageWarning + $"Class, record or struct '{typeName}' not found in package {packageId}.";
    }

    private static bool IsMatchingType(ApiTypeModel type, string typeName)
    {
        if (type.Kind is not (ApiTypeKind.Class or ApiTypeKind.StaticClass or ApiTypeKind.AbstractClass
            or ApiTypeKind.SealedClass or ApiTypeKind.RecordClass or ApiTypeKind.Struct or ApiTypeKind.RecordStruct))
        {
            return false;
        }

        return ApiModelSearch.Matches(type, typeName);
    }

}
