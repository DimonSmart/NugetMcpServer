using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

/// <summary>
/// Response model for interface listing including package version information
/// </summary>
public class InterfaceListResult
{
    /// <summary>
    /// NuGet package ID
    /// </summary>
    public string PackageId { get; set; } = string.Empty;
    /// <summary>
    /// Package version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// List of interfaces found in the package
    /// </summary>
    public List<InterfaceInfo> Interfaces { get; set; } = [];

    /// <summary>
    /// Returns a formatted string representation of the interface list
    /// </summary>
    /// <returns>Formatted list of interfaces</returns>
    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/* INTERFACES FROM {PackageId} v{Version} */");
        sb.AppendLine();
        var groupedInterfaces = Interfaces
            .GroupBy(i => i.AssemblyName)
            .OrderBy(g => g.Key);

        foreach (var group in groupedInterfaces)
        {
            sb.AppendLine($"## {group.Key}");

            foreach (var iface in group.OrderBy(i => i.FullName))
            {
                var formattedName = iface.GetFormattedFullName();
                sb.AppendLine($"- {formattedName}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
