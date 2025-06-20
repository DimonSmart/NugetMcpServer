using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

public class ClassListResult
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<ClassInfo> Classes { get; set; } = [];

    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/* CLASSES FROM {PackageId} v{Version} */");
        sb.AppendLine();
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
