using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

public class InterfaceListResult : PackageResultBase
{
    public List<InterfaceInfo> Interfaces { get; set; } = [];

    public override string ToFormattedString()
    {
        var sb = new StringBuilder();
        
        if (!HandleMetaPackageFormatting(sb, Interfaces.Count, "interfaces"))
        {
            return sb.ToString();
        }
        
        if (!IsMetaPackage)
        {
            sb.AppendLine($"/* INTERFACES FROM {PackageId} v{Version} */");
            sb.AppendLine();
            
            if (Interfaces.Count == 0)
            {
                sb.AppendLine("No public interfaces found in this package.");
                sb.AppendLine();
                return sb.ToString();
            }
        }

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
