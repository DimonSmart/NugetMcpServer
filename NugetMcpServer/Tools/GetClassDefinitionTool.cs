using System;
using System.ComponentModel;
using System.IO;
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
    ClassFormattingService formattingService,
    ArchiveProcessingService archiveService) : McpToolBase<GetClassDefinitionTool>(logger, packageService)
{
    [McpServerTool]
    [Description("Extracts and returns the C# class or record definition from a specified NuGet package.")]
    public Task<string> get_class_or_record_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Class or record name (short like 'Point' or full like 'System.Point')")] string typeName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetClassOrRecordDefinitionCore(packageId, typeName, version, progressNotifier),
            Logger,
            "Error fetching class or record definition");
    }

    private async Task<string> GetClassOrRecordDefinitionCore(
        string packageId,
        string typeName,
        string? version,
        ProgressNotifier progress)
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

        (LoadedPackageAssemblies loaded, PackageInfo packageInfo, string resolvedVersion) =
            await archiveService.LoadPackageAssembliesAsync(packageId, version, progress);

        Logger.LogInformation(
            "Fetching class or record {ClassName} from package {PackageId} version {Version}",
            typeName, packageId, resolvedVersion);

        string metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(packageInfo, packageId, resolvedVersion);

        progress.ReportMessage("Scanning assemblies for class/record");

        foreach (LoadedAssemblyInfo assemblyInfo in loaded.Assemblies)
        {
            progress.ReportMessage($"Scanning {assemblyInfo.FileName}: {assemblyInfo.PackagePath}");
            try
            {
                Type? classType = assemblyInfo.Types
                        .FirstOrDefault(t =>
                        {
                            if (!t.IsClass || !(t.IsPublic || t.IsNestedPublic))
                            {
                                return false;
                            }

                            if (t.Name == typeName)
                            {
                                return true;
                            }

                            if (t.FullName == typeName)
                            {
                                return true;
                            }

                            if (!t.IsGenericType)
                            {
                                return false;
                            }

                            int backtickIndex = t.Name.IndexOf('`');
                            if (backtickIndex > 0)
                            {
                                string baseName = t.Name.Substring(0, backtickIndex);
                                if (baseName == typeName)
                                {
                                    return true;
                                }
                            }

                            if (t.FullName != null)
                            {
                                int fullBacktickIndex = t.FullName.IndexOf('`');
                                if (fullBacktickIndex > 0)
                                {
                                    string fullBaseName = t.FullName.Substring(0, fullBacktickIndex);
                                    return fullBaseName == typeName;
                                }
                            }

                            return false;
                        });

                if (classType != null)
                {
                    progress.ReportMessage($"Class or record found: {typeName}");
                    string formatted = formattingService.FormatClassDefinition(classType, assemblyInfo.FileName, packageId, assemblyInfo.AssemblyBytes);
                    return metaPackageWarning + formatted;
                }
            }
            catch (FileNotFoundException)
            {
                // Missing referenced assembly - skip logging
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error processing assembly {AssemblyName}", assemblyInfo.FileName);
            }
        }

        return metaPackageWarning + $"Class or record '{typeName}' not found in package {packageId}.";
    }

}
