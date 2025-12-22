using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetMcpServer.Common;
using NuGetMcpServer.Extensions;
using NuGetMcpServer.Models;

namespace NuGetMcpServer.Services;

/// <summary>
/// Service for comparing two versions of a NuGet package
/// </summary>
public class PackageComparisonService
{
    private readonly ArchiveProcessingService _archiveProcessingService;
    private readonly DocumentationProvider _documentationProvider;
    private readonly ILogger<PackageComparisonService> _logger;

    public PackageComparisonService(
        ArchiveProcessingService archiveProcessingService,
        DocumentationProvider documentationProvider,
        ILogger<PackageComparisonService> logger)
    {
        _archiveProcessingService = archiveProcessingService;
        _documentationProvider = documentationProvider;
        _logger = logger;
    }

    /// <summary>
    /// Compare two versions of a package and detect all changes
    /// </summary>
    /// <param name="packageId">Package identifier</param>
    /// <param name="fromVersion">Older version</param>
    /// <param name="toVersion">Newer version</param>
    /// <param name="typeNameFilter">Optional wildcard filter for type names (classes, structs, records, interfaces). Filters by type name, not field/property names.</param>
    /// <param name="memberNameFilter">Optional wildcard filter for member names (properties, fields, methods). Supports OR via '|'. Filters by member name, not type names.</param>
    /// <param name="breakingChangesOnly">If true, returns only breaking changes (high severity)</param>
    /// <param name="maxChangesPerCategory">Maximum changes to return per category</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<ComparisonResult> CompareVersionsAsync(
        string packageId,
        string fromVersion,
        string toVersion,
        string? typeNameFilter = null,
        string? memberNameFilter = null,
        bool breakingChangesOnly = false,
        int maxChangesPerCategory = 100,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithPackageContextsAsync(
            packageId, fromVersion, toVersion,
            (oldLoaded, newLoaded) => CompareLoadedPackages(
                oldLoaded, newLoaded, packageId, fromVersion, toVersion,
                typeNameFilter, memberNameFilter, breakingChangesOnly, maxChangesPerCategory));
    }

    private async Task<T> ExecuteWithPackageContextsAsync<T>(
        string packageId,
        string fromVersion,
        string toVersion,
        Func<LoadedPackageAssemblies, LoadedPackageAssemblies, T> action)
    {
        LoadedPackageAssemblies? oldLoaded = null;
        LoadedPackageAssemblies? newLoaded = null;

        try
        {
            var progress = ProgressNotifier.VoidProgressNotifier;

            // Load both versions with separate load contexts
            _logger.LogInformation("Loading old version {FromVersion}", fromVersion);
            (oldLoaded, _, _) = await _archiveProcessingService.LoadPackageAssembliesAsync(
                packageId, fromVersion, progress);

            _logger.LogInformation("Loading new version {ToVersion}", toVersion);
            (newLoaded, _, _) = await _archiveProcessingService.LoadPackageAssembliesAsync(
                packageId, toVersion, progress);

            if (oldLoaded.Assemblies.Count == 0 || newLoaded.Assemblies.Count == 0)
            {
                throw new InvalidOperationException("No assemblies found in one or both package versions");
            }

            return action(oldLoaded, newLoaded);
        }
        finally
        {
            // Unload contexts to free memory
            oldLoaded?.AssemblyLoadContext.Unload();
            newLoaded?.AssemblyLoadContext.Unload();

            // Force GC to clean up unloaded assemblies
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private ComparisonResult CompareLoadedPackages(
        LoadedPackageAssemblies oldLoaded,
        LoadedPackageAssemblies newLoaded,
        string packageId,
        string fromVersion,
        string toVersion,
        string? typeNameFilter,
        string? memberNameFilter,
        bool breakingChangesOnly,
        int maxChangesPerCategory)
    {
        // Get all public types from both versions
        var oldTypes = GetPublicTypes(oldLoaded.Assemblies, typeNameFilter);
        var newTypes = GetPublicTypes(newLoaded.Assemblies, typeNameFilter);

        _logger.LogInformation(
            "Found {OldCount} types in old version, {NewCount} types in new version",
            oldTypes.Count, newTypes.Count);

        // Create type comparer
        var comparer = new TypeComparer(_documentationProvider);

        // Detect changes
        var changes = new List<TypeChange>();

        // Build dictionaries by full name for comparison
        var oldTypeDict = oldTypes.ToDictionary(t => t.FullName ?? t.Name);
        var newTypeDict = newTypes.ToDictionary(t => t.FullName ?? t.Name);

        // Find removed types
        foreach (var (fullName, type) in oldTypeDict)
        {
            if (!newTypeDict.ContainsKey(fullName))
            {
                changes.Add(comparer.CreateTypeRemovedChange(type));
            }
        }

        // Find added types
        foreach (var (fullName, type) in newTypeDict)
        {
            if (!oldTypeDict.ContainsKey(fullName))
            {
                changes.Add(comparer.CreateTypeAddedChange(type));
            }
        }

        // Compare existing types
        foreach (var (fullName, oldType) in oldTypeDict)
        {
            if (newTypeDict.TryGetValue(fullName, out var newType))
            {
                var typeChanges = comparer.CompareTypes(oldType, newType);
                changes.AddRange(typeChanges);
            }
        }

        _logger.LogInformation("Detected {ChangeCount} total changes", changes.Count);

        // Store all changes for summary (before any filtering)
        var allChanges = changes.ToList();

        // Apply memberNameFilter if requested
        if (!string.IsNullOrWhiteSpace(memberNameFilter))
        {
            var patterns = memberNameFilter.Split('|', StringSplitOptions.RemoveEmptyEntries);
            var regexes = patterns.Select(p => {
                var pattern = "^" + Regex.Escape(p.Trim())
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                return new Regex(pattern, RegexOptions.IgnoreCase);
            }).ToList();

            changes = changes.Where(c =>
                !string.IsNullOrEmpty(c.MemberName) &&
                regexes.Any(r => r.IsMatch(c.MemberName))
            ).ToList();

            _logger.LogInformation("Filtered to {Count} changes by member name", changes.Count);
        }

        // Apply breakingChangesOnly filter if requested
        if (breakingChangesOnly)
        {
            changes = changes.Where(c => c.Severity == ChangeSeverity.High).ToList();
            _logger.LogInformation("Filtered to {Count} breaking changes only", changes.Count);
        }

        // Apply per-category limits
        var limitedChanges = ApplyLimits(changes, maxChangesPerCategory);
        var isTruncated = limitedChanges.Count < changes.Count;

        // Build result
        return new ComparisonResult
        {
            PackageId = packageId,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            Changes = limitedChanges,
            Summary = BuildSummary(allChanges), // Use full changes for accurate summary
            IsTruncated = isTruncated,
            TypeNameFilter = typeNameFilter,
            MemberNameFilter = memberNameFilter,
            BreakingChangesOnly = breakingChangesOnly
        };
    }

    private List<Type> GetPublicTypes(List<LoadedAssemblyInfo> assemblies, string? typeNameFilter)
    {
        var types = new List<Type>();
        Regex? filterRegex = null;

        if (!string.IsNullOrWhiteSpace(typeNameFilter))
        {
            // Convert wildcard pattern to regex
            var pattern = "^" + Regex.Escape(typeNameFilter).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            filterRegex = new Regex(pattern, RegexOptions.IgnoreCase);
        }

        foreach (var assemblyInfo in assemblies)
        {
            var assemblyTypes = assemblyInfo.Types
                .Where(t => t.IsPublic || t.IsNestedPublic)
                .Where(t => !t.Name.StartsWith("<")) // Skip compiler-generated types
                .Where(t => filterRegex == null ||
                           filterRegex.IsMatch(t.FullName ?? t.Name) ||
                           filterRegex.IsMatch(t.Name));

            types.AddRange(assemblyTypes);
        }

        return types;
    }

    private List<TypeChange> ApplyLimits(List<TypeChange> changes, int maxPerCategory)
    {
        var result = new List<TypeChange>();
        var countByCategory = new Dictionary<ChangeCategory, int>();

        foreach (var change in changes)
        {
            if (!countByCategory.ContainsKey(change.Category))
            {
                countByCategory[change.Category] = 0;
            }

            if (countByCategory[change.Category] < maxPerCategory)
            {
                result.Add(change);
                countByCategory[change.Category]++;
            }
        }

        return result;
    }

    private ComparisonSummary BuildSummary(List<TypeChange> changes)
    {
        var summary = new ComparisonSummary
        {
            TotalChanges = changes.Count
        };

        // Categorize changes
        var breakingCategories = new HashSet<ChangeCategory>
        {
            ChangeCategory.TypeRemoved,
            ChangeCategory.MemberRemoved,
            ChangeCategory.MemberTypeChanged,
            ChangeCategory.MethodSignatureChanged,
            ChangeCategory.ParameterRemoved,
            ChangeCategory.ParameterTypeChanged,
            ChangeCategory.ReturnTypeChanged,
            ChangeCategory.BaseClassChanged,
            ChangeCategory.InterfaceRemoved,
            ChangeCategory.AccessibilityReduced,
            ChangeCategory.SealedAdded,
            ChangeCategory.AbstractAdded,
            ChangeCategory.VirtualRemoved,
            ChangeCategory.EnumValueRemoved,
            ChangeCategory.GenericParametersChanged
        };

        var additionCategories = new HashSet<ChangeCategory>
        {
            ChangeCategory.TypeAdded,
            ChangeCategory.MemberAdded,
            ChangeCategory.MethodOverloadAdded,
            ChangeCategory.ParameterAdded,
            ChangeCategory.InterfaceAdded,
            ChangeCategory.EnumValueAdded
        };

        var removalCategories = new HashSet<ChangeCategory>
        {
            ChangeCategory.TypeRemoved,
            ChangeCategory.MemberRemoved,
            ChangeCategory.InterfaceRemoved,
            ChangeCategory.EnumValueRemoved
        };

        foreach (var change in changes)
        {
            // Count by severity
            if (!summary.ChangesBySeverity.ContainsKey(change.Severity))
            {
                summary.ChangesBySeverity[change.Severity] = 0;
            }
            summary.ChangesBySeverity[change.Severity]++;

            // Count by category
            if (!summary.ChangesByCategory.ContainsKey(change.Category))
            {
                summary.ChangesByCategory[change.Category] = 0;
            }
            summary.ChangesByCategory[change.Category]++;

            // Categorize
            if (breakingCategories.Contains(change.Category))
            {
                summary.BreakingChanges++;
            }
            else if (additionCategories.Contains(change.Category))
            {
                summary.Additions++;
            }
            else
            {
                summary.NonBreakingChanges++;
            }

            if (removalCategories.Contains(change.Category))
            {
                summary.Removals++;
            }

            if (!additionCategories.Contains(change.Category) && !removalCategories.Contains(change.Category))
            {
                summary.Modifications++;
            }
        }

        return summary;
    }
}
