using System;
using System.Collections.Generic;
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
        [Description("Filter by name or namespace. Supports wildcards: '*Message*' matches any type containing 'Message', 'Telegram.Bot.Types.*' matches namespace")] string? filter = null,
        [Description("Maximum number of results to return (default: 100, prevents token limit issues)")] int maxResults = 100,
        [Description("Number of results to skip (for pagination)")] int skip = 0,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);

        return ExecuteWithLoggingAsync(
            () => ListTypesCore(packageId, version, filter, maxResults, skip, progressNotifier),
            Logger,
            "Error listing types");
    }

    private async Task<TypeListResult> ListTypesCore(string packageId, string? version, string? filter, int maxResults, int skip, IProgressNotifier progress)
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

        var allTypes = new List<TypeInfo>();

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

                allTypes.Add(new TypeInfo
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

        // Apply filter if provided
        var filteredTypes = allTypes;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filteredTypes = FilterTypes(allTypes, filter);
            progress.ReportMessage($"Filter applied: {filteredTypes.Count} types match '{filter}'");
        }

        // Apply pagination
        result.TotalCount = filteredTypes.Count;
        result.Types = filteredTypes.Skip(skip).Take(maxResults).ToList();
        result.ReturnedCount = result.Types.Count;
        result.IsPartial = result.ReturnedCount < result.TotalCount;

        progress.ReportMessage($"Type listing completed - Returned {result.ReturnedCount} of {result.TotalCount} types");

        return result;
    }

    private static List<TypeInfo> FilterTypes(List<TypeInfo> types, string filter)
    {
        // Convert wildcard pattern to regex
        // *Message* -> .*Message.*
        // Telegram.Bot.Types.* -> Telegram\.Bot\.Types\..*
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(filter)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return types.Where(t =>
            regex.IsMatch(t.Name) || regex.IsMatch(t.FullName)
        ).ToList();
    }
}
