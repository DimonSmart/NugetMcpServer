using System.Collections.Generic;
using NuGetMcpServer.Models;

namespace NuGetMcpServer.Services;

/// <summary>
/// Result of comparing two package versions
/// </summary>
public class ComparisonResult
{
    /// <summary>
    /// Package identifier
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Older version being compared
    /// </summary>
    public string FromVersion { get; set; } = string.Empty;

    /// <summary>
    /// Newer version being compared
    /// </summary>
    public string ToVersion { get; set; } = string.Empty;

    /// <summary>
    /// All detected changes
    /// </summary>
    public List<TypeChange> Changes { get; set; } = new();

    /// <summary>
    /// Summary statistics
    /// </summary>
    public ComparisonSummary Summary { get; set; } = new();

    /// <summary>
    /// Whether this comparison was truncated due to limits
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Type name filter applied during comparison (if any). Filters by type names (classes, structs, records, interfaces), not field/property names.
    /// </summary>
    public string? TypeNameFilter { get; set; }

    /// <summary>
    /// Member name filter applied during comparison (if any). Filters by member names (properties, fields, methods), not type names.
    /// </summary>
    public string? MemberNameFilter { get; set; }

    /// <summary>
    /// Whether only breaking changes were included in results
    /// </summary>
    public bool BreakingChangesOnly { get; set; }
}

/// <summary>
/// Summary statistics for a comparison
/// </summary>
public class ComparisonSummary
{
    /// <summary>
    /// Total number of changes detected
    /// </summary>
    public int TotalChanges { get; set; }

    /// <summary>
    /// Number of breaking changes
    /// </summary>
    public int BreakingChanges { get; set; }

    /// <summary>
    /// Number of non-breaking changes
    /// </summary>
    public int NonBreakingChanges { get; set; }

    /// <summary>
    /// Number of additions
    /// </summary>
    public int Additions { get; set; }

    /// <summary>
    /// Number of removals
    /// </summary>
    public int Removals { get; set; }

    /// <summary>
    /// Number of modifications
    /// </summary>
    public int Modifications { get; set; }

    /// <summary>
    /// Changes grouped by category
    /// </summary>
    public Dictionary<ChangeCategory, int> ChangesByCategory { get; set; } = new();

    /// <summary>
    /// Changes grouped by severity
    /// </summary>
    public Dictionary<ChangeSeverity, int> ChangesBySeverity { get; set; } = new();
}
