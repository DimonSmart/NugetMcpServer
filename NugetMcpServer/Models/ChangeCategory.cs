namespace NuGetMcpServer.Models;

/// <summary>
/// Categories of changes between package versions
/// </summary>
public enum ChangeCategory
{
    // Breaking changes - type level
    TypeRemoved,
    BaseClassChanged,
    InterfaceRemoved,
    SealedAdded,
    AbstractAdded,
    GenericParametersChanged,

    // Breaking changes - member level
    MemberRemoved,
    MemberTypeChanged,
    MethodSignatureChanged,
    ParameterRemoved,
    ParameterTypeChanged,
    ReturnTypeChanged,
    VirtualRemoved,
    AccessibilityReduced,

    // Breaking changes - enum
    EnumValueRemoved,

    // Non-breaking changes
    MemberObsoleted,
    AccessibilityExpanded,
    ParameterDefaultChanged,

    // Additions
    TypeAdded,
    MemberAdded,
    MethodOverloadAdded,
    ParameterAdded,
    InterfaceAdded,
    EnumValueAdded,

    // Metadata changes
    DependencyVersionChanged,
    DependencyAdded,
    DependencyRemoved,
    TargetFrameworkAdded,
    TargetFrameworkRemoved
}

/// <summary>
/// Severity level of a change
/// </summary>
public enum ChangeSeverity
{
    /// <summary>
    /// Low severity - unlikely to break existing code
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity - may break existing code in some scenarios
    /// </summary>
    Medium,

    /// <summary>
    /// High severity - likely to break existing code
    /// </summary>
    High
}
