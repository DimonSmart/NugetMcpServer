using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services;

public class InterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;

    // Converts generic notation from `N to <T> for display
    public string GetFormattedName() => Name.FormatGenericTypeName();

    // Converts generic notation from `N to <T> for display with namespace
    public string GetFormattedFullName() => FullName.FormatFullGenericTypeName();
}
