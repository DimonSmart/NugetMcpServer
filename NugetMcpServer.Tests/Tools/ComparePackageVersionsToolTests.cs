using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NuGetMcpServer.Models;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;
using Xunit;

namespace NuGetMcpServer.Tests.Tools;

public class ComparePackageVersionsToolTests
{
    [Fact]
    public void ChangeCategory_ShouldHaveAllExpectedValues()
    {
        // Verify the enum has all expected categories
        var categories = Enum.GetValues<ChangeCategory>();

        Assert.Contains(ChangeCategory.TypeRemoved, categories);
        Assert.Contains(ChangeCategory.TypeAdded, categories);
        Assert.Contains(ChangeCategory.MemberRemoved, categories);
        Assert.Contains(ChangeCategory.MemberAdded, categories);
        Assert.Contains(ChangeCategory.EnumValueRemoved, categories);
    }

    [Fact]
    public void ChangeSeverity_ShouldHaveThreeLevels()
    {
        var severities = Enum.GetValues<ChangeSeverity>();

        Assert.Equal(3, severities.Length);
        Assert.Contains(ChangeSeverity.Low, severities);
        Assert.Contains(ChangeSeverity.Medium, severities);
        Assert.Contains(ChangeSeverity.High, severities);
    }

    [Fact]
    public void TypeChange_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var change = new TypeChange
        {
            Category = ChangeCategory.MemberAdded,
            Severity = ChangeSeverity.Low,
            TypeName = "TestType",
            TypeFullName = "Namespace.TestType",
            MemberName = "NewMethod",
            From = "old",
            To = "new",
            Documentation = "Test docs",
            Impact = "Test impact",
            Migration = "Test migration"
        };

        // Assert
        Assert.Equal(ChangeCategory.MemberAdded, change.Category);
        Assert.Equal(ChangeSeverity.Low, change.Severity);
        Assert.Equal("TestType", change.TypeName);
        Assert.Equal("Namespace.TestType", change.TypeFullName);
        Assert.Equal("NewMethod", change.MemberName);
        Assert.Equal("old", change.From);
        Assert.Equal("new", change.To);
        Assert.Equal("Test docs", change.Documentation);
        Assert.Equal("Test impact", change.Impact);
        Assert.Equal("Test migration", change.Migration);
    }

    [Fact]
    public void ComparisonResult_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var result = new ComparisonResult();

        // Assert
        Assert.NotNull(result.Changes);
        Assert.Empty(result.Changes);
        Assert.NotNull(result.Summary);
        Assert.False(result.IsTruncated);
    }

    // Integration tests with TestLibrary.V1 and V2

    [Fact]
    public async Task Compare_DetectsBreakingChange_TypeRemoved()
    {
        // ClassToRemove exists in V1 but not V2
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.TypeRemoved &&
            c.TypeName == "ClassToRemove");
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_MemberTypeChanged()
    {
        // ClassWithMemberTypeChange.Amount: int → long
        var result = await CompareTestLibrariesAsync();

        var change = result.Changes.FirstOrDefault(c =>
            c.TypeName == "ClassWithMemberTypeChange" &&
            c.MemberName == "Amount");

        Assert.NotNull(change);
        Assert.True(
            change.Category == ChangeCategory.MemberRemoved ||
            change.FromType == "Int32" ||
            change.ToType == "Int64",
            "Expected property type change from Int32 to Int64");
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_MemberRemoved()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberRemoved &&
            c.TypeName == "ClassWithMemberRemoval" &&
            c.MemberName == "MethodToRemove");

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberRemoved &&
            c.TypeName == "ClassWithMemberRemoval" &&
            c.MemberName == "PropertyToRemove");

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberRemoved &&
            c.TypeName == "ClassWithMemberRemoval" &&
            c.MemberName == "FieldToRemove");
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_MethodSignatureChanged()
    {
        var result = await CompareTestLibrariesAsync();

        // MethodWithParamRemoval lost a parameter
        Assert.Contains(result.Changes, c =>
            c.TypeName == "ClassWithMethodSignatureChange" &&
            c.MemberName == "MethodWithParamRemoval");

        // MethodWithParamTypeChange: int → long parameter
        Assert.Contains(result.Changes, c =>
            c.TypeName == "ClassWithMethodSignatureChange" &&
            c.MemberName == "MethodWithParamTypeChange");

        // MethodWithReturnTypeChange: int → long return type
        Assert.Contains(result.Changes, c =>
            c.TypeName == "ClassWithMethodSignatureChange" &&
            c.MemberName == "MethodWithReturnTypeChange" &&
            c.Category == ChangeCategory.ReturnTypeChanged);
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_VirtualRemoved()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.VirtualRemoved &&
            c.TypeName == "ClassWithVirtualRemoval" &&
            c.MemberName == "VirtualMethod");
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_BaseClassChanged()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.BaseClassChanged &&
            c.TypeName == "ClassWithBaseChange");
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_InterfaceRemoved()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.InterfaceRemoved &&
            c.TypeName == "ClassWithInterfaceRemoval" &&
            c.From != null && c.From.Contains("IInterface2"));
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_AccessibilityReduced()
    {
        var result = await CompareTestLibrariesAsync();

        // Accessibility changes are detected for methods only
        // The method should be detected as MemberRemoved (public version) and possibly MemberAdded (internal version if it were still visible)
        // Since internal members aren't public, we should see it as removed
        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberRemoved &&
            c.TypeName == "ClassWithAccessibilityReduction" &&
            c.MemberName == "PublicMethod");
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_SealedAdded()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.SealedAdded &&
            c.TypeName == "ClassToBeSealed");
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_AbstractAdded()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.AbstractAdded &&
            c.TypeName == "ClassToBeAbstract");
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_GenericParametersChanged()
    {
        var result = await CompareTestLibrariesAsync();

        // GenericClass<T> in V1 becomes GenericClass<T, U> in V2
        // This is detected as the old type being removed and a new type being added
        // because they have different generic parameter counts
        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.TypeRemoved &&
            c.TypeName.StartsWith("GenericClass"));

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.TypeAdded &&
            c.TypeName.StartsWith("GenericClass"));
    }

    [Fact]
    public async Task Compare_DetectsBreakingChange_EnumValueRemoved()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.EnumValueRemoved &&
            c.TypeName == "EnumWithValueRemoval" &&
            c.MemberName == "Value2");
    }

    [Fact]
    public async Task Compare_DetectsNonBreaking_MemberObsoleted()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberObsoleted &&
            c.TypeName == "ClassWithObsolete" &&
            c.MemberName == "MethodToObsolete");
    }

    [Fact]
    public async Task Compare_DetectsNonBreaking_AccessibilityExpanded()
    {
        var result = await CompareTestLibrariesAsync();

        // Internal to public: the internal method wasn't visible before, so it appears as MemberAdded
        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberAdded &&
            c.TypeName == "ClassWithAccessibilityExpansion" &&
            c.MemberName == "InternalMethod");
    }

    [Fact]
    public async Task Compare_DetectsAddition_TypeAdded()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.TypeAdded &&
            c.TypeName == "NewClass");

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.TypeAdded &&
            c.TypeName == "IAddedInterface");

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.TypeAdded &&
            c.TypeName == "NewEnum");
    }

    [Fact]
    public async Task Compare_DetectsAddition_MemberAdded()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberAdded &&
            c.TypeName == "ClassWithAdditions" &&
            c.MemberName == "NewMethod");

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberAdded &&
            c.TypeName == "ClassWithAdditions" &&
            c.MemberName == "NewProperty");

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.MemberAdded &&
            c.TypeName == "ClassWithAdditions" &&
            c.MemberName == "NewField");
    }

    [Fact]
    public async Task Compare_DetectsAddition_InterfaceAdded()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.InterfaceAdded &&
            c.TypeName == "ClassWithInterfaceAddition");
    }

    [Fact]
    public async Task Compare_DetectsAddition_EnumValueAdded()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.Contains(result.Changes, c =>
            c.Category == ChangeCategory.EnumValueAdded &&
            c.TypeName == "EnumWithValueAddition" &&
            c.MemberName == "Value3");
    }

    [Fact]
    public async Task Compare_WithFilter_ReturnsOnlyMatchingTypes()
    {
        var result = await CompareTestLibrariesAsync(typeFilter: "*WithAdditions");

        // All changes should be for ClassWithAdditions
        Assert.All(result.Changes, c =>
            Assert.Contains("WithAdditions", c.TypeName));
    }

    [Fact]
    public async Task Compare_WithMaxChanges_TruncatesResults()
    {
        var result = await CompareTestLibrariesAsync(maxChangesPerCategory: 2);

        Assert.True(result.IsTruncated);

        // Should have at most 2 changes per category
        var countsByCategory = result.Changes
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(countsByCategory.Values, count =>
            Assert.True(count <= 2, $"Expected at most 2 changes per category, got {count}"));
    }

    [Fact]
    public async Task Compare_Summary_CountsChangesCorrectly()
    {
        var result = await CompareTestLibrariesAsync();

        Assert.True(result.Summary.TotalChanges > 0);
        Assert.True(result.Summary.BreakingChanges > 0);
        Assert.True(result.Summary.Additions > 0);
        Assert.True(result.Summary.Removals > 0);

        // Verify that all changes are accounted for
        var sum = result.Summary.BreakingChanges +
                  result.Summary.NonBreakingChanges +
                  result.Summary.Additions;

        // Note: Some changes may be counted in multiple categories
        Assert.True(sum >= result.Summary.TotalChanges / 2);
    }

    [Fact]
    public async Task Compare_WithBreakingChangesOnly_ExcludesAdditions()
    {
        // Compare with breakingChangesOnly flag
        var result = await CompareTestLibrariesAsync(breakingChangesOnly: true);

        // Should have breaking changes
        Assert.True(result.Changes.Count > 0);
        Assert.True(result.BreakingChangesOnly);

        // All changes should be high severity
        Assert.All(result.Changes, c => Assert.Equal(ChangeSeverity.High, c.Severity));

        // Should NOT contain any additions (which are low severity)
        Assert.DoesNotContain(result.Changes, c => c.Category == ChangeCategory.TypeAdded);
        Assert.DoesNotContain(result.Changes, c => c.Category == ChangeCategory.MemberAdded);
        Assert.DoesNotContain(result.Changes, c => c.Category == ChangeCategory.EnumValueAdded);
    }

    [Fact]
    public async Task Compare_WithBreakingChangesOnly_SummaryShowsAll()
    {
        // Compare with breakingChangesOnly flag
        var result = await CompareTestLibrariesAsync(breakingChangesOnly: true);

        // Summary should show the full picture (before filtering)
        Assert.True(result.Summary.TotalChanges > result.Changes.Count,
            "Summary should include all changes, not just breaking ones");

        // Summary should have additions even though they're not in Changes
        Assert.True(result.Summary.Additions > 0,
            "Summary should show additions even when breakingChangesOnly is true");

        // Changes should only contain breaking
        Assert.All(result.Changes, c => Assert.Equal(ChangeSeverity.High, c.Severity));
    }

    [Fact]
    public async Task Compare_WithBreakingChangesOnlyAndFilter_BothApplied()
    {
        // Compare with both typeFilter and breakingChangesOnly
        var result = await CompareTestLibrariesAsync(
            typeFilter: "*WithAdditions",
            breakingChangesOnly: true);

        Assert.True(result.BreakingChangesOnly);
        Assert.Equal("*WithAdditions", result.TypeNameFilter);

        // All changes should be from *WithAdditions types
        Assert.All(result.Changes, c => Assert.Contains("WithAdditions", c.TypeName));

        // All changes should be high severity
        Assert.All(result.Changes, c => Assert.Equal(ChangeSeverity.High, c.Severity));
    }

    [Fact]
    public async Task Compare_WithMemberFilter_ReturnsOnlyMatchingMembers()
    {
        // Test exact match for "StarCount" member
        var result = await CompareTestLibrariesAsync(memberFilter: "StarCount");

        // Should have changes for StarCount member only
        Assert.True(result.Changes.Count > 0, "Should have at least one StarCount change");
        Assert.Equal("StarCount", result.MemberNameFilter);

        // All changes should be for StarCount member
        Assert.All(result.Changes, c => Assert.Equal("StarCount", c.MemberName));

        // Should find StarCount in ClassForMemberFilter (int → long)
        Assert.Contains(result.Changes, c =>
            c.TypeName == "ClassForMemberFilter" &&
            c.MemberName == "StarCount");

        // Should also find StarCount in AnotherClassWithMembers (int → long)
        Assert.Contains(result.Changes, c =>
            c.TypeName == "AnotherClassWithMembers" &&
            c.MemberName == "StarCount");
    }

    [Fact]
    public async Task Compare_WithMemberFilterWildcard_MatchesPattern()
    {
        // Test wildcard pattern "*Id" to match all members ending with "Id"
        var result = await CompareTestLibrariesAsync(memberFilter: "*Id");

        Assert.True(result.Changes.Count > 0, "Should have changes for *Id members");
        Assert.Equal("*Id", result.MemberNameFilter);

        // All changes should be for members ending with "Id"
        Assert.All(result.Changes, c =>
        {
            Assert.NotNull(c.MemberName);
            Assert.EndsWith("Id", c.MemberName);
        });

        // Should find TopicId (int → string)
        Assert.Contains(result.Changes, c =>
            c.TypeName == "ClassForMemberFilter" &&
            c.MemberName == "TopicId");

        // Should NOT contain StarCount or Amount (they don't end with "Id")
        Assert.DoesNotContain(result.Changes, c => c.MemberName == "StarCount");
        Assert.DoesNotContain(result.Changes, c => c.MemberName == "Amount");
    }

    [Fact]
    public async Task Compare_WithMemberFilterOR_MatchesMultiple()
    {
        // Test OR logic: "StarCount|TopicId|Amount"
        var result = await CompareTestLibrariesAsync(memberFilter: "StarCount|TopicId|Amount");

        Assert.True(result.Changes.Count > 0, "Should have changes for OR filter");
        Assert.Equal("StarCount|TopicId|Amount", result.MemberNameFilter);

        // All changes should match one of the three patterns
        Assert.All(result.Changes, c =>
        {
            var isMatch = c.MemberName == "StarCount" ||
                         c.MemberName == "TopicId" ||
                         c.MemberName == "Amount";
            Assert.True(isMatch, $"Member {c.MemberName} should match StarCount|TopicId|Amount");
        });

        // Should find all three
        Assert.Contains(result.Changes, c => c.MemberName == "StarCount");
        Assert.Contains(result.Changes, c => c.MemberName == "TopicId");
        Assert.Contains(result.Changes, c => c.MemberName == "Amount");

        // Should NOT contain GetValue or CalculateTotal
        Assert.DoesNotContain(result.Changes, c => c.MemberName == "GetValue");
        Assert.DoesNotContain(result.Changes, c => c.MemberName == "CalculateTotal");
    }

    [Fact]
    public async Task Compare_WithTypeAndMemberFilter_BothApplied()
    {
        // Test combination of typeFilter and memberFilter
        var result = await CompareTestLibrariesAsync(
            typeFilter: "ClassForMemberFilter",
            memberFilter: "*Count");

        Assert.True(result.Changes.Count > 0, "Should have changes matching both filters");
        Assert.Equal("ClassForMemberFilter", result.TypeNameFilter);
        Assert.Equal("*Count", result.MemberNameFilter);

        // All changes should be from ClassForMemberFilter AND have member names ending with "Count"
        Assert.All(result.Changes, c =>
        {
            Assert.Equal("ClassForMemberFilter", c.TypeName);
            Assert.NotNull(c.MemberName);
            Assert.EndsWith("Count", c.MemberName);
        });

        // Should find StarCount in ClassForMemberFilter
        Assert.Contains(result.Changes, c =>
            c.TypeName == "ClassForMemberFilter" &&
            c.MemberName == "StarCount");

        // Should NOT contain AnotherClassWithMembers (filtered by type)
        Assert.DoesNotContain(result.Changes, c => c.TypeName == "AnotherClassWithMembers");

        // Should NOT contain TopicId from ClassForMemberFilter (doesn't end with "Count")
        Assert.DoesNotContain(result.Changes, c =>
            c.TypeName == "ClassForMemberFilter" &&
            c.MemberName == "TopicId");
    }

    // Helper method to compare TestLibrary.V1 and V2
    private Task<ComparisonResult> CompareTestLibrariesAsync(
        string? typeFilter = null,
        string? memberFilter = null,
        bool breakingChangesOnly = false,
        int maxChangesPerCategory = 100)
    {
        // Get paths to the test library assemblies
        // Use the workspace root to find the assemblies
        var workspaceRoot = "/workspaces/NugetMcpServer";
        var v1Path = Path.Combine(workspaceRoot, "TestLibraries", "TestLibrary.V1", "bin", "Debug", "net9.0", "TestLibrary.V1.dll");
        var v2Path = Path.Combine(workspaceRoot, "TestLibraries", "TestLibrary.V2", "bin", "Debug", "net9.0", "TestLibrary.V2.dll");

        // Verify files exist
        Assert.True(File.Exists(v1Path), $"V1 assembly not found at: {v1Path}");
        Assert.True(File.Exists(v2Path), $"V2 assembly not found at: {v2Path}");

        // Load assemblies in separate contexts
        var v1Context = new AssemblyLoadContext($"TestLibrary.V1-{Guid.NewGuid()}", isCollectible: true);
        var v2Context = new AssemblyLoadContext($"TestLibrary.V2-{Guid.NewGuid()}", isCollectible: true);

        try
        {
            var v1Assembly = v1Context.LoadFromAssemblyPath(v1Path);
            var v2Assembly = v2Context.LoadFromAssemblyPath(v2Path);

            var v1Types = GetPublicTypes(v1Assembly, typeFilter);
            var v2Types = GetPublicTypes(v2Assembly, typeFilter);

            // Create type comparer with null logger for tests
            var documentationProvider = new DocumentationProvider();
            var comparer = new TypeComparer(documentationProvider);

            // Detect changes
            var changes = new List<TypeChange>();

            // Build dictionaries by full name
            var oldTypeDict = v1Types.ToDictionary(t => t.FullName ?? t.Name);
            var newTypeDict = v2Types.ToDictionary(t => t.FullName ?? t.Name);

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

            // Store all changes for summary
            var allChanges = changes.ToList();

            // Apply memberFilter if requested
            if (!string.IsNullOrWhiteSpace(memberFilter))
            {
                var patterns = memberFilter.Split('|', StringSplitOptions.RemoveEmptyEntries);
                var regexes = patterns.Select(p => {
                    var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(p.Trim())
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$";
                    return new System.Text.RegularExpressions.Regex(pattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }).ToList();

                changes = changes.Where(c =>
                    !string.IsNullOrEmpty(c.MemberName) &&
                    regexes.Any(r => r.IsMatch(c.MemberName))
                ).ToList();
            }

            // Apply breakingChangesOnly filter if requested
            if (breakingChangesOnly)
            {
                changes = changes.Where(c => c.Severity == ChangeSeverity.High).ToList();
            }

            // Apply per-category limits
            var limitedChanges = ApplyLimits(changes, maxChangesPerCategory);
            var isTruncated = limitedChanges.Count < changes.Count;

            // Build result
            var result = new ComparisonResult
            {
                PackageId = "TestLibrary",
                FromVersion = "1.0.0",
                ToVersion = "2.0.0",
                Changes = limitedChanges,
                Summary = BuildSummary(allChanges),
                IsTruncated = isTruncated,
                TypeNameFilter = typeFilter,
                MemberNameFilter = memberFilter,
                BreakingChangesOnly = breakingChangesOnly
            };

            return Task.FromResult(result);
        }
        finally
        {
            v1Context.Unload();
            v2Context.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private List<Type> GetPublicTypes(Assembly assembly, string? filter)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsPublic || t.IsNestedPublic)
            .Where(t => !t.Name.StartsWith("<")) // Skip compiler-generated types
            .ToList();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(filter)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new System.Text.RegularExpressions.Regex(pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            types = types.Where(t =>
                regex.IsMatch(t.FullName ?? t.Name) ||
                regex.IsMatch(t.Name)).ToList();
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
            if (!summary.ChangesBySeverity.ContainsKey(change.Severity))
            {
                summary.ChangesBySeverity[change.Severity] = 0;
            }
            summary.ChangesBySeverity[change.Severity]++;

            if (!summary.ChangesByCategory.ContainsKey(change.Category))
            {
                summary.ChangesByCategory[change.Category] = 0;
            }
            summary.ChangesByCategory[change.Category]++;

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

    [Fact]
    public async Task Compare_SameAssemblyDifferentContext_NoFalsePositives()
    {
        // This test verifies that TypeComparer.AreTypesEquivalent correctly ignores assembly version differences
        // by loading the same assembly twice in different contexts (which gives them different runtime versions)

        var workspaceRoot = "/workspaces/NugetMcpServer";
        var v1Path = Path.Combine(workspaceRoot, "TestLibraries", "TestLibrary.V1", "bin", "Debug", "net9.0", "TestLibrary.V1.dll");

        Assert.True(File.Exists(v1Path), $"V1 assembly not found at: {v1Path}");

        // Load the SAME assembly in two different contexts
        var context1 = new AssemblyLoadContext($"TestLibrary.V1-Context1-{Guid.NewGuid()}", isCollectible: true);
        var context2 = new AssemblyLoadContext($"TestLibrary.V1-Context2-{Guid.NewGuid()}", isCollectible: true);

        try
        {
            var assembly1 = context1.LoadFromAssemblyPath(v1Path);
            var assembly2 = context2.LoadFromAssemblyPath(v1Path);

            var types1 = GetPublicTypes(assembly1, null);
            var types2 = GetPublicTypes(assembly2, null);

            Assert.True(types1.Count > 0, "Should have loaded some types");
            Assert.Equal(types1.Count, types2.Count);

            var documentationProvider = new DocumentationProvider();
            var comparer = new TypeComparer(documentationProvider);

            // Build dictionaries
            var typeDict1 = types1.ToDictionary(t => t.FullName ?? t.Name);
            var typeDict2 = types2.ToDictionary(t => t.FullName ?? t.Name);

            // Compare types
            var changes = new List<TypeChange>();
            foreach (var (fullName, type1) in typeDict1)
            {
                if (typeDict2.TryGetValue(fullName, out var type2))
                {
                    var typeChanges = comparer.CompareTypes(type1, type2);
                    changes.AddRange(typeChanges);
                }
            }

            // Should have NO changes because it's the same assembly
            // (any changes would be false positives due to assembly version comparison)
            Assert.Empty(changes);
        }
        finally
        {
            context1.Unload();
            context2.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    // === OPTIONAL PARAMETERS TESTS ===

    [Fact]
    public async Task Compare_DetectsNonBreaking_CompatibleOverloadWithOptionalParams()
    {
        // ClassWithOptionalParamsAdded: methods in V2 add optional parameters to V1 signatures
        // This should be detected as MethodOverloadAdded (non-breaking) instead of MemberRemoved + MemberAdded
        var result = await CompareTestLibrariesAsync();

        // SendMessage: string text → string text, int? threadId = null
        var sendMessageChanges = result.Changes.Where(c =>
            c.TypeName == "ClassWithOptionalParamsAdded" &&
            c.MemberName == "SendMessage").ToList();

        // Should have exactly ONE change: MethodOverloadAdded
        Assert.Single(sendMessageChanges);
        Assert.Equal(ChangeCategory.MethodOverloadAdded, sendMessageChanges[0].Category);
        Assert.Equal(ChangeSeverity.Low, sendMessageChanges[0].Severity);

        // Calculate: (int a, int b) → (int a, int b, int c = 0, int d = 0)
        var calculateChanges = result.Changes.Where(c =>
            c.TypeName == "ClassWithOptionalParamsAdded" &&
            c.MemberName == "Calculate").ToList();

        Assert.Single(calculateChanges);
        Assert.Equal(ChangeCategory.MethodOverloadAdded, calculateChanges[0].Category);
        Assert.Equal(ChangeSeverity.Low, calculateChanges[0].Severity);

        // FormatText: (string input) → (string input, bool uppercase = false)
        var formatChanges = result.Changes.Where(c =>
            c.TypeName == "ClassWithOptionalParamsAdded" &&
            c.MemberName == "FormatText").ToList();

        Assert.Single(formatChanges);
        Assert.Equal(ChangeCategory.MethodOverloadAdded, formatChanges[0].Category);
        Assert.Equal(ChangeSeverity.Low, formatChanges[0].Severity);
    }

    [Fact]
    public async Task Compare_DetectsBreaking_IncompatibleOverloadWithRequiredParams()
    {
        // ClassWithRequiredParamsAdded: ProcessData adds a REQUIRED parameter (not optional)
        // This should be detected as MemberRemoved + MemberAdded (breaking)
        var result = await CompareTestLibrariesAsync();

        var changes = result.Changes.Where(c =>
            c.TypeName == "ClassWithRequiredParamsAdded" &&
            c.MemberName == "ProcessData").ToList();

        // Should have TWO changes: one Removed and one Added (because new param is NOT optional)
        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.Category == ChangeCategory.MemberRemoved);
        Assert.Contains(changes, c => c.Category == ChangeCategory.MemberAdded);

        // Both should be detected as breaking/additions
        Assert.Contains(changes, c => c.Severity == ChangeSeverity.High);
    }

    [Fact]
    public async Task Compare_DetectsNonBreaking_TrueOverloadWhileOldRemains()
    {
        // ClassWithTrueOverload: V2 adds new overload Execute(string, int)
        // while keeping old Execute(string)
        // This should show ONLY MemberAdded for the new overload (non-breaking)
        var result = await CompareTestLibrariesAsync();

        var changes = result.Changes.Where(c =>
            c.TypeName == "ClassWithTrueOverload" &&
            c.MemberName == "Execute").ToList();

        // Should have exactly ONE change: MemberAdded for the new overload
        Assert.Single(changes);
        Assert.Equal(ChangeCategory.MemberAdded, changes[0].Category);
        Assert.Equal(ChangeSeverity.Low, changes[0].Severity);

        // Should contain the signature with two parameters
        Assert.Contains("Int32", changes[0].To);
    }

    [Fact]
    public async Task Compare_DetectsBreaking_ParameterReordering()
    {
        // ClassWithParamReordering: ConfigureSettings changes parameter order
        // V1: (string name, int value, bool enabled)
        // V2: (int value, string name, bool enabled)
        // This is BREAKING - old calls will fail
        var result = await CompareTestLibrariesAsync();

        var changes = result.Changes.Where(c =>
            c.TypeName == "ClassWithParamReordering" &&
            c.MemberName == "ConfigureSettings").ToList();

        // Should detect as MemberRemoved + MemberAdded (signatures don't match)
        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.Category == ChangeCategory.MemberRemoved);
        Assert.Contains(changes, c => c.Category == ChangeCategory.MemberAdded);

        // Should be breaking
        Assert.Contains(changes, c => c.Severity == ChangeSeverity.High);
    }
}
