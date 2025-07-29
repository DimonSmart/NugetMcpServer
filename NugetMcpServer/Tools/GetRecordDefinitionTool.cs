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
public class GetRecordDefinitionTool(
    ILogger<GetRecordDefinitionTool> logger,
    NuGetPackageService packageService,
    ClassFormattingService formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetRecordDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# record definition from a specified NuGet package.")]
    public Task<string> get_record_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Record name (short or full name)")] string recordName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetRecordDefinitionCore(packageId, recordName, version, progressNotifier),
            Logger,
            "Error fetching record definition");
    }

    private async Task<string> GetRecordDefinitionCore(
        string packageId,
        string recordName,
        string? version,
        ProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        if (string.IsNullOrWhiteSpace(recordName))
            throw new ArgumentNullException(nameof(recordName));

        var (loaded, packageInfo, resolvedVersion) =
            await archiveService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Fetching record {RecordName} from package {PackageId} version {Version}",
            recordName, packageId, resolvedVersion);

        var metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(packageInfo, packageId, resolvedVersion);

        progress.ReportMessage("Scanning assemblies for record");

        foreach (var assemblyInfo in loaded.Assemblies)
        {
            try
            {
                var recordType = assemblyInfo.Types.FirstOrDefault(t => TypeFormattingHelpers.IsRecordType(t) && (t.Name == recordName || t.FullName == recordName || GenericMatch(t, recordName)));
                if (recordType != null)
                {
                    progress.ReportMessage($"Record found: {recordName}");
                    var formatted = formattingService.FormatClassDefinition(recordType, assemblyInfo.FileName, packageId, assemblyInfo.AssemblyBytes);
                    return metaPackageWarning + formatted;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error processing assembly {AssemblyName}", assemblyInfo.FileName);
            }
        }

        return metaPackageWarning + $"Record '{recordName}' not found in package {packageId}.";
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
