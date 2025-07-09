using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

public class ClassListResult : PackageResultBase
{
    public List<ClassInfo> Classes { get; set; } = [];

    public override string ToFormattedString()
    {
        var sb = new StringBuilder();
        
        if (!HandleMetaPackageFormatting(sb, Classes.Count, "classes"))
        {
            return sb.ToString();
        }
        
        if (!IsMetaPackage)
        {
            sb.AppendLine($"/* CLASSES FROM {PackageId} v{Version} */");
            sb.AppendLine();
            
            if (Classes.Count == 0)
            {
                sb.AppendLine("No public classes found in this package.");
                sb.AppendLine();
                return sb.ToString();
            }
        }

        var groupedClasses = Classes
            .GroupBy(c => c.AssemblyName)
            .OrderBy(g => g.Key);

        foreach (var group in groupedClasses)
        {
            sb.AppendLine($"## {group.Key}");

            foreach (var cls in group.OrderBy(c => c.FullName))
            {
                var formattedName = cls.GetFormattedFullName();
                var modifiers = new List<string>();

                if (cls.IsStatic) modifiers.Add("static");
                if (cls.IsAbstract) modifiers.Add("abstract");
                if (cls.IsSealed) modifiers.Add("sealed");

                var modifierString = modifiers.Count > 0 ? $" ({string.Join(", ", modifiers)})" : "";
                sb.AppendLine($"- {formattedName}{modifierString}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
