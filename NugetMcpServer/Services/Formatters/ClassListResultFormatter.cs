using System.Collections.Generic;
using System.Linq;
using System.Text;

using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services.Formatters;

public static class ClassListResultFormatter
{
    public static string Format(this ClassListResult result)
    {
        var sb = new StringBuilder();

        sb.Append(result.GetMetaPackageWarningIfAny());

        if (result.ShouldShowMetaPackageWarningOnly(result.Classes.Count))
        {
            return sb.ToString();
        }

        if (!result.IsMetaPackage)
        {
            sb.AppendLine($"Classes from {result.PackageId} v{result.Version}");
            sb.AppendLine(new string('=', $"Classes from {result.PackageId} v{result.Version}".Length));
            sb.AppendLine();
        }

        if (result.Classes.Count == 0)
        {
            sb.AppendLine("No public classes found in this package.");
            return sb.ToString();
        }

        if (result.IsMetaPackage && result.Classes.Count > 0)
        {
            sb.AppendLine("This meta-package also contains the following classes:");
            sb.AppendLine();
        }

        var groupedClasses = result.Classes
            .GroupBy(c => c.AssemblyName)
            .OrderBy(g => g.Key);

        foreach (var group in groupedClasses)
        {
            sb.AppendLine($"## {group.Key}");

            foreach (var cls in group.OrderBy(c => c.FullName))
            {
                var formattedName = cls.FullName.FormatGenericTypeName();
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
