using System.Linq;
using System.Text;
using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services.Formatters;

public static class StructListResultFormatter
{
    public static string Format(this StructListResult result)
    {
        var sb = new StringBuilder();
        sb.Append(result.GetMetaPackageWarningIfAny());

        if (result.ShouldShowMetaPackageWarningOnly(result.Structs.Count))
            return sb.ToString();

        if (!result.IsMetaPackage)
        {
            sb.AppendLine($"Structs from {result.PackageId} v{result.Version}");
            sb.AppendLine(new string('=', $"Structs from {result.PackageId} v{result.Version}".Length));
            sb.AppendLine();
        }

        if (result.Structs.Count == 0)
        {
            sb.AppendLine("No public structs found in this package.");
            return sb.ToString();
        }

        if (result.IsMetaPackage && result.Structs.Count > 0)
        {
            sb.AppendLine("This meta-package also contains the following structs:");
            sb.AppendLine();
        }

        var grouped = result.Structs.GroupBy(s => s.AssemblyName).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key}");
            foreach (var st in group.OrderBy(s => s.FullName))
            {
                var formattedName = st.FullName.FormatGenericTypeName();
                sb.AppendLine($"- {formattedName}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
