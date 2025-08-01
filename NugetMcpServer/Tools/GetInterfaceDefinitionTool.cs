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
    InterfaceFormattingService formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetInterfaceDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# interface definition from a specified NuGet package.")]
    public Task<string> get_interface_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Interface name (short name like 'IDisposable' or full name like 'System.IDisposable')")] string interfaceName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetInterfaceDefinitionCore(packageId, interfaceName, version, progressNotifier),
            Logger,
            "Error fetching interface definition");
    }

    private async Task<string> GetInterfaceDefinitionCore(
        string packageId,
        string interfaceName,
        string? version,
        ProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new ArgumentNullException(nameof(interfaceName));
        }

        var (loaded, packageInfo, resolvedVersion) =
            await archiveService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Fetching interface {InterfaceName} from package {PackageId} version {Version}",
            interfaceName, packageId, resolvedVersion);

        var metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(packageInfo);

        foreach (var assemblyInfo in loaded.Assemblies)
        {
            progress.ReportMessage($"Scanning {assemblyInfo.FileName}: {assemblyInfo.PackagePath}");

            try
            {
                var iface = assemblyInfo.Types
                        .FirstOrDefault(t =>
                        {
                            if (!t.IsInterface)
                            {
                                return false;
                            }

                            if (t.Name == interfaceName)
                            {
                                return true;
                            }

                            if (t.FullName == interfaceName)
                            {
                                return true;
                            }

                            if (!t.IsGenericType)
                            {
                                return false;
                            }

                            var backtickIndex = t.Name.IndexOf('`');
                            if (backtickIndex > 0)
                            {
                                var baseName = t.Name.Substring(0, backtickIndex);
                                if (baseName == interfaceName)
                                {
                                    return true;
                                }
                            }

                            if (t.FullName != null)
                            {
                                var fullBacktickIndex = t.FullName.IndexOf('`');
                                if (fullBacktickIndex > 0)
                                {
                                    var fullBaseName = t.FullName.Substring(0, fullBacktickIndex);
                                    return fullBaseName == interfaceName;
                                }
                            }

                            return false;
                        });

                if (iface != null)
                {
                    progress.ReportMessage($"Interface found: {interfaceName}");
                    var formatted = formattingService.FormatInterfaceDefinition(iface, assemblyInfo.FileName, packageId);
                    return metaPackageWarning + formatted;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error processing assembly {AssemblyName}", assemblyInfo.FileName);
            }
        }

        return metaPackageWarning + $"Interface '{interfaceName}' not found in package {packageId}.";
    }
}
