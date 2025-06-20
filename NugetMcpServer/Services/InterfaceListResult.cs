using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

public class InterfaceListResult
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<InterfaceInfo> Interfaces { get; set; } = [];

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
