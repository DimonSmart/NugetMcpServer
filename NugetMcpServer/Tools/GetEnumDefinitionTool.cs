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
        using var progressNotifier = new ProgressNotifier(progress);
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

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        Logger.LogInformation("Fetching enum {EnumName} from package {PackageId} version {Version}", enumName, packageId, version!);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);

        var metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(packageInfo, packageId, version!);

        progress.ReportMessage("Scanning assemblies for enum");

        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        var loadedAssemblies = await archiveService.LoadAllAssembliesFromPackageAsync(packageReader);

        foreach (var assemblyInfo in loadedAssemblies)
        {
            var definition = TryGetEnumFromAssembly(assemblyInfo, enumName, packageId);
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
            var enumType = assemblyInfo.Types
                .FirstOrDefault(t => t.IsEnum && (t.Name == enumName || t.FullName == enumName));

            if (enumType == null)
            {
                return null;
            }

            return formattingService.FormatEnumDefinition(enumType, assemblyInfo.AssemblyName, packageId);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error processing assembly {AssemblyName}", assemblyInfo.AssemblyName);
            return null;
        }
    }
}
