namespace NuGetMcpServer.Models;

/// <summary>
/// Represents a change detected between two versions of a package
/// </summary>
public class TypeChange
{
    /// <summary>
    /// Category of the change
    /// </summary>
    public ChangeCategory Category { get; set; }

    /// <summary>
    /// Severity of the change
    /// </summary>
    public ChangeSeverity Severity { get; set; }

    /// <summary>
    /// Simple name of the affected type
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the affected type including namespace
    /// </summary>
    public string? TypeFullName { get; set; }

    /// <summary>
    /// Name of the affected member (method, property, field, etc.)
    /// </summary>
    public string? MemberName { get; set; }

    /// <summary>
    /// Original value/signature (for modifications)
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// New value/signature (for modifications)
    /// </summary>
    public string? To { get; set; }

    /// <summary>
    /// Original type (for type changes)
    /// </summary>
    public string? FromType { get; set; }

    /// <summary>
    /// New type (for type changes)
    /// </summary>
    public string? ToType { get; set; }

    /// <summary>
    /// XML documentation comment for the changed member
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// Description of the impact of this change
    /// </summary>
    public string? Impact { get; set; }

    /// <summary>
    /// Suggested migration path for breaking changes
    /// </summary>
    public string? Migration { get; set; }
}
