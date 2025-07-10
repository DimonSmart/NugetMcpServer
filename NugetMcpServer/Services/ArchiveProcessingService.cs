using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuGet.Packaging;

namespace NuGetMcpServer.Services;

/// <summary>
/// Represents the result of loading an assembly from a package file.
/// </summary>
public record LoadedAssemblyInfo
{
    public Assembly Assembly { get; init; } = null!;
    public Type[] Types { get; init; } = [];
    public string AssemblyName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
}

public class ArchiveProcessingService(ILogger<ArchiveProcessingService> logger, NuGetPackageService packageService)
{
    private readonly ILogger<ArchiveProcessingService> _logger = logger;
    private readonly NuGetPackageService _packageService = packageService;

    /// <summary>
    /// Loads assembly and types from a package file. Returns null if assembly cannot be loaded.
    /// </summary>
    /// <param name="packageReader">The PackageArchiveReader to read files from</param>
    /// <param name="filePath">Path to the assembly file within the package</param>
    /// <returns>LoadedAssemblyInfo with assembly, types and metadata, or null if loading fails</returns>
    public LoadedAssemblyInfo? LoadAssemblyFromPackageFile(PackageArchiveReader packageReader, string filePath)
    {
        try
        {
            using var fileStream = packageReader.GetStream(filePath);
            using var ms = new MemoryStream();
            fileStream.CopyTo(ms);

            var assemblyData = ms.ToArray();
            var (assembly, types) = _packageService.LoadAssemblyFromMemoryWithTypes(assemblyData);

            if (assembly == null)
                return null;

            var assemblyName = Path.GetFileName(filePath);
            return new LoadedAssemblyInfo
            {
                Assembly = assembly,
                Types = types,
                AssemblyName = assemblyName,
                FilePath = filePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error loading assembly from package file {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Asynchronously loads assembly and types from a package file. Returns null if assembly cannot be loaded.
    /// </summary>
    /// <param name="packageReader">The PackageArchiveReader to read files from</param>
    /// <param name="filePath">Path to the assembly file within the package</param>
    /// <returns>LoadedAssemblyInfo with assembly, types and metadata, or null if loading fails</returns>
    public async Task<LoadedAssemblyInfo?> LoadAssemblyFromPackageFileAsync(PackageArchiveReader packageReader, string filePath)
    {
        try
        {
            using var fileStream = packageReader.GetStream(filePath);
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);

            var assemblyData = ms.ToArray();
            var (assembly, types) = _packageService.LoadAssemblyFromMemoryWithTypes(assemblyData);

            if (assembly == null)
                return null;

            var assemblyName = Path.GetFileName(filePath);
            return new LoadedAssemblyInfo
            {
                Assembly = assembly,
                Types = types,
                AssemblyName = assemblyName,
                FilePath = filePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error loading assembly from package file {FilePath}", filePath);
            return null;
        }
    }

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
    /// <returns>List of LoadedAssemblyInfo for all successfully loaded assemblies</returns>
    public List<LoadedAssemblyInfo> LoadAllAssembliesFromPackage(PackageArchiveReader packageReader)
    {
        var dllFiles = GetUniqueAssemblyFiles(packageReader);
        var result = new List<LoadedAssemblyInfo>();

        foreach (var filePath in dllFiles)
        {
            var assemblyInfo = LoadAssemblyFromPackageFile(packageReader, filePath);
            if (assemblyInfo != null)
            {
                result.Add(assemblyInfo);
            }
        }

        return result;
    }

    /// <summary>
    /// Asynchronously loads all assemblies from unique DLL files in the package.
    /// </summary>
    /// <param name="packageReader">The PackageArchiveReader to read files from</param>
    /// <returns>List of LoadedAssemblyInfo for all successfully loaded assemblies</returns>
    public async Task<List<LoadedAssemblyInfo>> LoadAllAssembliesFromPackageAsync(PackageArchiveReader packageReader)
    {
        var dllFiles = GetUniqueAssemblyFiles(packageReader);
        var result = new List<LoadedAssemblyInfo>();

        foreach (var filePath in dllFiles)
        {
            var assemblyInfo = await LoadAssemblyFromPackageFileAsync(packageReader, filePath);
            if (assemblyInfo != null)
            {
                result.Add(assemblyInfo);
            }
        }

        return result;
    }
}
