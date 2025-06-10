using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services;

/// <summary>
/// Model for class information
/// </summary>
public class ClassInfo
{
    /// <summary>
    /// Class name (without namespace)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full class name with namespace
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Assembly name where class is defined
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if the class is static
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Indicates if the class is abstract
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Indicates if the class is sealed
    /// </summary>
    public bool IsSealed { get; set; }

    /// <summary>
    /// Returns a formatted name for display, converting generic notation from `N to <T>
    /// </summary>
    /// <returns>Formatted class name</returns>
    public string GetFormattedName() => Name.FormatGenericTypeName();

    /// <summary>
    /// Returns a formatted full name for display, converting generic notation from `N to <T>
    /// </summary>
    /// <returns>Formatted full class name</returns>
    public string GetFormattedFullName() => FullName.FormatGenericTypeName();
}
