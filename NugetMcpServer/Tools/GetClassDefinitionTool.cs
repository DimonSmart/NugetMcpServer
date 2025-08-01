using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
    [Description("Extracts and returns the C# class, record or struct definition from a specified NuGet package.")]
    public Task<string> get_class_or_record_or_struct_definition(
        [Description("NuGet package ID")] string packageId,
        [Description("Class, record or struct name (short like 'Point' or full like 'System.Point')")] string typeName,
        [Description("Package version (optional, defaults to latest)")] string? version = null,
        [Description("Progress notification for long-running operations")] IProgress<ProgressNotificationValue>? progress = null)
    {
        using ProgressNotifier progressNotifier = new ProgressNotifier(progress);
        return ExecuteWithLoggingAsync(
            () => GetClassOrRecordDefinitionCore(packageId, typeName, version, progressNotifier),
            Logger,
            "Error fetching class, record or struct definition");
    }

    [RequiresAssemblyFiles("Calls NuGetMcpServer.Services.ClassFormattingService.FormatClassDefinition(Type, String, String, Byte[])")]
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
            "Fetching class, record or struct {ClassName} from package {PackageId} version {Version}",
            typeName, packageId, resolvedVersion);

        string metaPackageWarning = MetaPackageHelper.CreateMetaPackageWarning(packageInfo);

        progress.ReportMessage("Scanning assemblies for class/record/struct");

        foreach (LoadedAssemblyInfo assemblyInfo in loaded.Assemblies)
        {
            progress.ReportMessage($"Scanning {assemblyInfo.FileName}: {assemblyInfo.PackagePath}");
            try
            {
                var classType = assemblyInfo.Types
                    .FirstOrDefault(t => IsMatchingType(t, typeName));

                if (classType != null)
                {
                    progress.ReportMessage($"Class, record or struct found: {typeName}");
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

        return metaPackageWarning + $"Class, record or struct '{typeName}' not found in package {packageId}.";
    }

    private static bool IsMatchingType(Type type, string typeName)
    {
        if (!((type.IsClass || (type.IsValueType && !type.IsEnum)) && (type.IsPublic || type.IsNestedPublic)))
            return false;

        if (type.Name == typeName || type.FullName == typeName)
            return true;

        if (!type.IsGenericType)
            return false;

        // Check generic type name without backtick
        var backtickIndex = type.Name.IndexOf('`');
        if (backtickIndex > 0)
        {
            var baseName = type.Name.Substring(0, backtickIndex);
            if (baseName == typeName)
                return true;
        }

        // Check full generic type name without backtick
        if (type.FullName != null)
        {
            var fullBacktickIndex = type.FullName.IndexOf('`');
            if (fullBacktickIndex > 0)
            {
                var fullBaseName = type.FullName.Substring(0, fullBacktickIndex);
                return fullBaseName == typeName;
            }
        }

        return false;
    }

}
