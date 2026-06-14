using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services;

public class ArchiveProcessingService(
    ILogger<ArchiveProcessingService> logger,
    NuGetPackageService packageService,
    PackageMetadataReader metadataReader)
{
    private readonly ILogger<ArchiveProcessingService> _logger = logger;
    private readonly NuGetPackageService _packageService = packageService;
    private readonly PackageMetadataReader _metadataReader = metadataReader;

    public static List<string> GetUniqueAssemblyFiles(PackageArchiveReader packageReader)
    {
        var assemblyFiles = GetCandidateAssemblyFiles(packageReader.GetFiles()).ToList();
        return SelectNearestAssemblyFiles(assemblyFiles)
            .Select(static file => file.PackagePath)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<LoadedPackageMetadata> LoadPackageMetadataAsync(
        string packageId,
        string? version,
        IProgressNotifier progress,
        string? source = null)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentNullException(nameof(packageId));
        }

        progress.ReportMessage("Resolving package version");
        if (version.IsNullOrEmptyOrNullString())
        {
            version = await _packageService.GetLatestVersion(packageId, source);
        }

        _logger.LogInformation("Reading metadata from package {PackageId} version {Version}", packageId, version);
        progress.ReportMessage($"Downloading package {packageId} v{version}");
        using var packageStream = await _packageService.DownloadPackageAsync(packageId, version!, progress, source);

        progress.ReportMessage("Extracting package information");
        var packageInfo = _packageService.GetPackageInfoAsync(packageStream, packageId, version!);

        progress.ReportMessage("Selecting package assemblies");
        packageStream.Position = 0;
        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        var assemblies = await ReadSelectedAssemblyFilesAsync(packageReader);

        progress.ReportMessage($"Reading metadata from {assemblies.Count} assemblies");
        var api = _metadataReader.ReadPackageApi(packageId, version!, assemblies);

        return new LoadedPackageMetadata
        {
            PackageId = packageId,
            Version = version!,
            PackageInfo = packageInfo,
            Api = api
        };
    }

    public async Task<IReadOnlyList<PackageAssemblyFile>> ReadSelectedAssemblyFilesAsync(PackageArchiveReader packageReader)
    {
        var candidateFiles = GetCandidateAssemblyFiles(packageReader.GetFiles()).ToList();
        var selectedFiles = SelectNearestAssemblyFiles(candidateFiles)
            .OrderBy(static file => file.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedFiles.Count == 0)
        {
            _logger.LogDebug("No lib target framework assemblies found in package");
            return [];
        }

        var result = new List<PackageAssemblyFile>();
        foreach (var file in selectedFiles)
        {
            using var stream = packageReader.GetStream(file.PackagePath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            result.Add(new PackageAssemblyFile
            {
                PackagePath = file.PackagePath,
                FileName = Path.GetFileName(file.PackagePath),
                TargetFramework = file.TargetFramework,
                Bytes = ms.ToArray()
            });
        }

        return result;
    }

    internal static IReadOnlyList<(string PackagePath, string TargetFramework)> SelectNearestAssemblyFiles(
        IReadOnlyList<(string PackagePath, string TargetFramework)> candidateFiles)
    {
        return candidateFiles
            .GroupBy(file => Path.GetFileNameWithoutExtension(file.PackagePath), StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                var selectedFramework = SelectNearestFramework(group.Select(static file => file.TargetFramework));
                return selectedFramework == null
                    ? []
                    : group.Where(file => file.TargetFramework.Equals(selectedFramework, StringComparison.OrdinalIgnoreCase));
            })
            .OrderBy(static file => file.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static NuGetFramework GetCurrentFramework()
    {
        return !string.IsNullOrWhiteSpace(AppContext.TargetFrameworkName)
            ? NuGetFramework.ParseFrameworkName(AppContext.TargetFrameworkName, DefaultFrameworkNameProvider.Instance)
            : NuGetFramework.ParseFolder($"net{Environment.Version.Major}.0");
    }

    private static IEnumerable<(string PackagePath, string TargetFramework)> GetCandidateAssemblyFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = file.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !parts[0].Equals("lib", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return (file, parts[1]);
        }
    }

    internal static string? SelectNearestFramework(IEnumerable<string> targetFrameworks)
    {
        var distinct = targetFrameworks
            .Where(static framework => !string.IsNullOrWhiteSpace(framework))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static framework => framework, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinct.Count == 0)
        {
            return null;
        }

        var reducer = new FrameworkReducer();
        var candidates = distinct
            .Select(framework => NuGetFramework.ParseFolder(framework))
            .Where(static framework => !framework.IsUnsupported)
            .ToList();

        var nearest = reducer.GetNearest(GetCurrentFramework(), candidates);
        return nearest?.GetShortFolderName() ?? distinct.First();
    }
}
