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
public class GetInterfaceDefinitionTool(
    ILogger<GetInterfaceDefinitionTool> logger,
    NuGetPackageService packageService,
    ApiDefinitionFormatter formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetInterfaceDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# interface definition from a specified NuGet package.")]
    public Task<string> get_interface_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Interface name (short name like 'IDisposable' or full name like 'System.IDisposable')")] string interfaceName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null,
        [Description("Optional NuGet source name or URL/path")] string? source = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetInterfaceDefinitionCore(packageId, interfaceName, version, progressNotifier, source),
            Logger,
            "Error fetching interface definition");
    }

    private async Task<string> GetInterfaceDefinitionCore(
        string packageId,
        string interfaceName,
        string? version,
        ProgressNotifier progress,
        string? source)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new ArgumentNullException(nameof(interfaceName));
        }

        LoadedPackageMetadata loaded =
            await archiveService.LoadPackageMetadataAsync(packageId, version, progress, source);

        Logger.LogInformation(
            "Fetching interface {InterfaceName} from package {PackageId} version {Version}",
            interfaceName, packageId, loaded.Version);

        var metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(loaded.PackageInfo);

        foreach (var assemblyInfo in loaded.Api.Assemblies)
        {
            progress.ReportMessage($"Scanning {assemblyInfo.FileName}: {assemblyInfo.PackagePath}");

            var iface = assemblyInfo.Types.FirstOrDefault(t =>
                t.Kind == ApiTypeKind.Interface && ApiModelSearch.Matches(t, interfaceName));
            if (iface != null)
            {
                progress.ReportMessage($"Interface found: {interfaceName}");
                var formatted = formattingService.FormatTypeDefinition(iface, packageId, loaded.Version, assemblyInfo.TargetFramework);
                return metaPackageWarning + formatted;
            }
        }

        return metaPackageWarning + $"Interface '{interfaceName}' not found in package {packageId}.";
    }
}
