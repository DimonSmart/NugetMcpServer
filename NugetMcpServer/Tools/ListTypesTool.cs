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
public class ListTypesTool(ILogger<ListTypesTool> logger, NuGetPackageService packageService, ArchiveProcessingService archiveProcessingService) : McpToolBase<ListTypesTool>(logger, packageService)
{
    private readonly ArchiveProcessingService _archiveProcessingService = archiveProcessingService;

    [McpServerTool]
    [Description("Lists all public classes, records and structs available in a specified NuGet package.")]
    public Task<TypeListResult> list_classes_records_structs(
        [Description("NuGet package ID")] string packageId,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => ListTypesCore(packageId, version, progressNotifier),
            Logger,
            "Error listing types");
    }

    private async Task<TypeListResult> ListTypesCore(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        (LoadedPackageAssemblies loaded, PackageInfo packageInfo, string resolvedVersion) =
            await _archiveProcessingService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Listing types from package {PackageId} version {Version}",
            packageId, resolvedVersion);

        var result = new TypeListResult
        {
            PackageId = packageId,
            Version = resolvedVersion,
            Types = []
        };

        result.IsMetaPackage = packageInfo.IsMetaPackage;
        result.Dependencies = packageInfo.Dependencies;
        result.Description = packageInfo.Description ?? string.Empty;

        progress.ReportMessage("Scanning assemblies for classes/records/structs");

        foreach (LoadedAssemblyInfo assemblyInfo in loaded.Assemblies)
        {
            var types = assemblyInfo.Types
                .Where(t =>
                    (t.IsClass || TypeFormattingHelpers.IsRecordType(t) || (t.IsValueType && !t.IsEnum)) &&
                    (t.IsPublic || t.IsNestedPublic))
                .ToList();

            foreach (var type in types)
            {
                var kind = TypeKind.Class;
                var isRecord = TypeFormattingHelpers.IsRecordType(type);
                var isStruct = type.IsValueType && !type.IsEnum;
                if (isRecord && isStruct)
                    kind = TypeKind.RecordStruct;
                else if (isRecord)
                    kind = TypeKind.RecordClass;
                else if (isStruct)
                    kind = TypeKind.Struct;

                result.Types.Add(new TypeInfo
                {
                    Name = type.Name,
                    FullName = type.FullName ?? string.Empty,
                    AssemblyName = assemblyInfo.FileName,
                    IsStatic = type.IsAbstract && type.IsSealed,
                    IsAbstract = type.IsAbstract && !type.IsSealed,
                    IsSealed = type.IsSealed && !type.IsAbstract,
                    Kind = kind
                });
            }
        }

        progress.ReportMessage($"Type listing completed - Found {result.Types.Count} types");

        return result;
    }
}
