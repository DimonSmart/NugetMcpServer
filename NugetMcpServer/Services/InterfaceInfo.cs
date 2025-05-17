using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services;

/// <summary>
/// Model for interface information
/// </summary>
public class InterfaceInfo
{
    /// <summary>
    /// Interface name (without namespace)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full interface name with namespace
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Assembly name where interface is defined
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Returns a formatted name for display, converting generic notation from `N to <T>
    /// </summary>
    /// <returns>Formatted interface name</returns>
    public string GetFormattedName() => Name.FormatGenericTypeName();

    /// <summary>
    /// Returns a formatted full name with namespace, converting generic notation
    /// </summary>
    /// <returns>Formatted full interface name with namespace</returns>
    public string GetFormattedFullName() => FullName.FormatFullGenericTypeName();
}
