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
    EnumFormattingService formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetEnumDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# enum definition from a specified NuGet package.")]
    public Task<string> get_enum_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Enum name (short name like 'DayOfWeek' or full name like 'System.DayOfWeek')")] string enumName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetEnumDefinitionCore(packageId, enumName, version, progressNotifier),
            Logger,
            "Error fetching enum definition");
    }
    private async Task<string> GetEnumDefinitionCore(
        string packageId,
        string enumName,
        string? version,
        ProgressNotifier progress)
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

        (LoadedPackageAssemblies loaded, PackageInfo packageInfo, string resolvedVersion) =
            await archiveService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Fetching enum {EnumName} from package {PackageId} version {Version}",
            enumName, packageId, resolvedVersion);

        string metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(packageInfo, packageId, resolvedVersion);

        progress.ReportMessage("Scanning assemblies for enum");

        foreach (LoadedAssemblyInfo assemblyInfo in loaded.Assemblies)
        {
            progress.ReportMessage($"Scanning {assemblyInfo.FileName}: {assemblyInfo.PackagePath}");
            string? definition = TryGetEnumFromAssembly(assemblyInfo, enumName, packageId);
            if (definition != null)
            {
                progress.ReportMessage($"Enum found: {enumName}");
                return metaPackageWarning + definition;
            }
        }

        return metaPackageWarning + $"Enum '{enumName}' not found in package {packageId}.";
    }

    private string? TryGetEnumFromAssembly(LoadedAssemblyInfo assemblyInfo, string enumName, string packageId)
    {
        try
        {
            Type? enumType = assemblyInfo.Types
                .FirstOrDefault(t => t.IsEnum && (t.Name == enumName || t.FullName == enumName));

            return enumType == null ? null : formattingService.FormatEnumDefinition(enumType, assemblyInfo.FileName, packageId);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error processing assembly {AssemblyName}", assemblyInfo.FileName);
            return null;
        }
    }
}
