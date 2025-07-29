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
public class GetClassDefinitionTool(
    ILogger<GetClassDefinitionTool> logger,
    NuGetPackageService packageService,
    ClassFormattingService formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetClassDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# class definition from a specified NuGet package.")]
    public Task<string> get_class_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Class name (short name like 'String' or full name like 'System.String')")] string className,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetClassDefinitionCore(packageId, className, version, progressNotifier),
            Logger,
            "Error fetching class definition");
    }

    private async Task<string> GetClassDefinitionCore(
        string packageId,
        string className,
        string? version,
        ProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentNullException(nameof(className));
        }

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await PackageService.GetLatestVersion(packageId);
        }

        Logger.LogInformation("Fetching class {ClassName} from package {PackageId} version {Version}",
            className, packageId, version!);

        progress.ReportMessage($"Downloading package {packageId} v{version}");

        using var packageStream = await PackageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = PackageService.GetPackageInfoAsync(packageStream, packageId, version!);

        var metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(packageInfo, packageId, version!);

        progress.ReportMessage("Scanning assemblies for class");

        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);

        var loadedAssemblies = await archiveService.LoadAllAssembliesFromPackageAsync(packageReader);

        foreach (var assemblyInfo in loadedAssemblies)
        {
            try
            {
                var classType = assemblyInfo.Types
                    .FirstOrDefault(t =>
                        {
                            if (!t.IsClass || !t.IsPublic)
                            {
                                return false;
                            }

                            if (t.Name == className)
                            {
                                return true;
                            }

                            if (t.FullName == className)
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
                                if (baseName == className)
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
                                    return fullBaseName == className;
                                }
                            }

                            return false;
                        });

                if (classType != null)
                {
                    progress.ReportMessage($"Class found: {className}");
                    var formatted = formattingService.FormatClassDefinition(classType, assemblyInfo.AssemblyName, packageId);
                    return metaPackageWarning + formatted;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error processing assembly {AssemblyName}", assemblyInfo.AssemblyName);
            }
        }

        return metaPackageWarning + $"Class '{className}' not found in package {packageId}.";
    }

}
