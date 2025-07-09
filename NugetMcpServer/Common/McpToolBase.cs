using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuGetMcpServer.Services;

namespace NuGetMcpServer.Common;

public abstract class McpToolBase<T>(ILogger<T> logger, NuGetPackageService packageService) where T : class
{
    protected readonly ILogger<T> Logger = logger;
    protected readonly NuGetPackageService PackageService = packageService;

    protected async Task<(Assembly? assembly, System.Type[] types)> LoadAssemblyFromEntryWithTypesAsync(ZipArchiveEntry entry)
    {
        using var entryStream = entry.Open();
        using var ms = new MemoryStream();
        await entryStream.CopyToAsync(ms);

        var assemblyData = ms.ToArray();
        return PackageService.LoadAssemblyFromMemoryWithTypes(assemblyData);
    }

    /// <summary>
    /// Filters DLL entries to avoid duplicates from different target frameworks.
    /// Prefers newer framework versions when multiple versions of the same assembly exist.
    /// </summary>
    protected static List<ZipArchiveEntry> FilterUniqueAssemblies(List<ZipArchiveEntry> dllEntries)
    {
        // Group by assembly name (filename without extension)
        var groupedByName = dllEntries
            .GroupBy(entry => Path.GetFileNameWithoutExtension(entry.Name), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<ZipArchiveEntry>();

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
                var bestEntry = SelectBestAssemblyEntry(group.ToList());
                if (bestEntry != null)
                {
                    result.Add(bestEntry);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Selects the best assembly entry from multiple versions.
    /// Priority: net8.0 > net6.0 > netstandard2.1 > netstandard2.0 > net4x > others
    /// </summary>
    private static ZipArchiveEntry? SelectBestAssemblyEntry(List<ZipArchiveEntry> entries)
    {
        if (!entries.Any()) return null;
        if (entries.Count == 1) return entries.First();

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

        var bestEntry = entries
            .Select(entry => new
            {
                Entry = entry,
                Framework = ExtractFrameworkFromPath(entry.FullName),
                Priority = GetFrameworkPriority(ExtractFrameworkFromPath(entry.FullName), frameworkPriorities)
            })
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Entry.FullName) // Consistent ordering for same priority
            .First();

        return bestEntry.Entry;
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
}
