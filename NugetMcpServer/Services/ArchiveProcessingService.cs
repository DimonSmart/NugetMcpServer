using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuGet.Packaging;
using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services;

/// <summary>
/// Represents the result of loading an assembly from a package file.
/// </summary>
public record LoadedAssemblyInfo
{
    public Assembly Assembly { get; init; } = null!;
    public Type[] Types { get; init; } = [];
    /// <summary>File name of the DLL inside the package.</summary>
    public string FileName { get; init; } = string.Empty;
    /// <summary>Relative path of the DLL inside the NuGet package.</summary>
    public string PackagePath { get; init; } = string.Empty;
    public byte[] AssemblyBytes { get; init; } = Array.Empty<byte>();
}

/// <summary>
/// Represents all assemblies loaded from a NuGet package along with the
/// <see cref="AssemblyLoadContext"/> that keeps them alive.
/// </summary>
public sealed record LoadedPackageAssemblies
{
    /// <summary>The context used to resolve assemblies.</summary>
    public AssemblyLoadContext AssemblyLoadContext { get; init; } = null!;

    /// <summary>Information for each successfully loaded assembly.</summary>
    public List<LoadedAssemblyInfo> Assemblies { get; init; } = [];
}

public class ArchiveProcessingService(ILogger<ArchiveProcessingService> logger, NuGetPackageService packageService)
{
    private readonly ILogger<ArchiveProcessingService> _logger = logger;
    private readonly NuGetPackageService _packageService = packageService;


    /// <summary>
    /// Gets a filtered list of unique DLL files from the package, avoiding duplicates from different target frameworks.
    /// Prefers newer framework versions when multiple versions of the same assembly exist.
    /// </summary>
    /// <param name="packageReader">The PackageArchiveReader to read files from</param>
    /// <returns>List of unique DLL file paths within the package</returns>
    public static List<string> GetUniqueAssemblyFiles(PackageArchiveReader packageReader)
    {
        var allFiles = packageReader.GetFiles();
        var dllFiles = allFiles.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();

        // Group by assembly name (filename without extension)
        var groupedByName = dllFiles
            .GroupBy(file => Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<string>();

        foreach (var group in groupedByName)
        {
            if (group.Count() == 1)
            {
                // Only one version, take it
                result.Add(group.First());
            }
            else
            {
                // Multiple versions - prefer the most appropriate one
                var bestFile = SelectBestAssemblyFile(group.ToList());
                if (bestFile != null)
                {
                    result.Add(bestFile);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Selects the best assembly file from multiple versions.
    /// Priority: net8.0 > net6.0 > netstandard2.1 > netstandard2.0 > net4x > others
    /// </summary>
    private static string? SelectBestAssemblyFile(List<string> files)
    {
        if (!files.Any()) return null;
        if (files.Count == 1) return files.First();

        // Define framework priority (higher number = higher priority)
        var frameworkPriorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "net8.0", 100 },
            { "net7.0", 90 },
            { "net6.0", 80 },
            { "netstandard2.1", 70 },
            { "netstandard2.0", 60 },
            { "net48", 50 },
            { "net472", 45 },
            { "net471", 44 },
            { "net47", 43 },
            { "net462", 42 },
            { "net461", 41 },
            { "net46", 40 },
            { "net452", 35 },
            { "net451", 34 },
            { "net45", 33 }
        };

        var bestFile = files
            .Select(file => new
            {
                File = file,
                Framework = ExtractFrameworkFromPath(file),
                Priority = GetFrameworkPriority(ExtractFrameworkFromPath(file), frameworkPriorities)
            })
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.File) // Consistent ordering for same priority
            .First();

        return bestFile.File;
    }

    /// <summary>
    /// Extracts target framework from assembly path like "lib/net6.0/Assembly.dll"
    /// </summary>
    private static string ExtractFrameworkFromPath(string fullName)
    {
        var pathParts = fullName.Split('/', '\\');

        // Look for lib/<framework>/ pattern
        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            if (pathParts[i].Equals("lib", StringComparison.OrdinalIgnoreCase) && i + 1 < pathParts.Length)
            {
                return pathParts[i + 1];
            }
        }

        return "unknown";
    }

    /// <summary>
    /// Gets priority for a framework, with fallback for unknown frameworks
    /// </summary>
    private static int GetFrameworkPriority(string framework, Dictionary<string, int> priorities)
    {
        if (priorities.TryGetValue(framework, out int priority))
        {
            return priority;
        }

        // Fallback logic for unknown frameworks
        if (framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            // Try to extract version number for newer .NET versions
            if (framework.Length > 3 && char.IsDigit(framework[3]))
            {
                return 20; // Medium priority for unrecognized newer .NET versions
            }
        }

        return 10; // Low priority for completely unknown frameworks
    }

    /// <summary>
    /// Loads all assemblies from unique DLL files in the package.
    /// </summary>
    /// <param name="packageReader">The PackageArchiveReader to read files from</param>
    /// <returns>Container with loaded assemblies and the context used to load them</returns>
    public LoadedPackageAssemblies LoadAllAssembliesFromPackage(PackageArchiveReader packageReader)
    {
        var dllFiles = GetUniqueAssemblyFiles(packageReader);
        var assemblies = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in dllFiles)
        {
            using var stream = packageReader.GetStream(filePath);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var name = Path.GetFileNameWithoutExtension(filePath);
            assemblies[name] = ms.ToArray();
            fileMap[name] = filePath;
        }

        var loadContext = new InMemoryAssemblyLoadContext(assemblies);
        var result = new List<LoadedAssemblyInfo>();

        foreach (var (name, bytes) in assemblies)
        {
            var (assembly, types) = _packageService.LoadAssemblyFromMemoryWithTypes(bytes, loadContext);
            if (assembly != null)
            {
                result.Add(new LoadedAssemblyInfo
                {
                    Assembly = assembly,
                    Types = types,
                    FileName = name + ".dll",
                    PackagePath = fileMap[name],
                    AssemblyBytes = bytes
                });
            }
        }

        return new LoadedPackageAssemblies
        {
            AssemblyLoadContext = loadContext,
            Assemblies = result
        };
    }

    /// <summary>
    /// Asynchronously loads all assemblies from unique DLL files in the package.
    /// </summary>
    /// <param name="packageReader">The PackageArchiveReader to read files from</param>
    /// <returns>Container with loaded assemblies and the context used to load them</returns>
    public async Task<LoadedPackageAssemblies> LoadAllAssembliesFromPackageAsync(PackageArchiveReader packageReader)
    {
        var dllFiles = GetUniqueAssemblyFiles(packageReader);
        var assemblies = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in dllFiles)
        {
            using var stream = packageReader.GetStream(filePath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var name = Path.GetFileNameWithoutExtension(filePath);
            assemblies[name] = ms.ToArray();
            fileMap[name] = filePath;
        }

        var loadContext = new InMemoryAssemblyLoadContext(assemblies);
        var result = new List<LoadedAssemblyInfo>();

        foreach (var (name, bytes) in assemblies)
        {
            var (assembly, types) = _packageService.LoadAssemblyFromMemoryWithTypes(bytes, loadContext);
            if (assembly != null)
            {
                result.Add(new LoadedAssemblyInfo
                {
                    Assembly = assembly,
                    Types = types,
                    FileName = name + ".dll",
                    PackagePath = fileMap[name],
                    AssemblyBytes = bytes
                });
            }
        }

        return new LoadedPackageAssemblies
        {
            AssemblyLoadContext = loadContext,
            Assemblies = result
        };
    }

    /// <summary>
    /// Resolves version, downloads the package and loads all assemblies.
    /// Also returns parsed package information.
    /// </summary>
    /// <param name="packageId">NuGet package ID</param>
    /// <param name="version">Optional package version</param>
    /// <param name="progress">Progress reporter</param>
    /// <returns>Tuple of loaded assemblies, package info and resolved version</returns>
    public async Task<(LoadedPackageAssemblies Assemblies, PackageInfo PackageInfo, string Version)>
        LoadPackageAssembliesAsync(string packageId, string? version, IProgressNotifier progress)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentNullException(nameof(packageId));

        progress.ReportMessage("Resolving package version");

        if (version.IsNullOrEmptyOrNullString())
        {
            version = await _packageService.GetLatestVersion(packageId);
        }

        _logger.LogInformation("Loading assemblies from package {PackageId} version {Version}",
            packageId, version);

        progress.ReportMessage($"Downloading package {packageId} v{version}");
        using var packageStream = await _packageService.DownloadPackageAsync(packageId, version!, progress);

        progress.ReportMessage("Extracting package information");
        var packageInfo = _packageService.GetPackageInfoAsync(packageStream, packageId, version!);

        progress.ReportMessage("Loading assemblies");
        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        var assemblies = await LoadAllAssembliesFromPackageAsync(packageReader);

        return (assemblies, packageInfo, version!);
    }
}
