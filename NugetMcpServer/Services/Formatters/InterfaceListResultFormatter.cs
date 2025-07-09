using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services.Formatters;

public static class InterfaceListResultFormatter
{
    public static string Format(this InterfaceListResult result)
    {
        var sb = new StringBuilder();

        sb.Append(result.GetMetaPackageWarningIfAny());

        if (result.ShouldShowMetaPackageWarningOnly(result.Interfaces.Count))
        {
            return sb.ToString();
        }

        if (!result.IsMetaPackage)
        {
            sb.AppendLine($"Interfaces from {result.PackageId} v{result.Version}");
            sb.AppendLine(new string('=', $"Interfaces from {result.PackageId} v{result.Version}".Length));
            sb.AppendLine();
        }

        if (result.Interfaces.Count == 0)
        {
            sb.AppendLine("No public interfaces found in this package.");
            return sb.ToString();
        }

        if (result.IsMetaPackage && result.Interfaces.Count > 0)
        {
            sb.AppendLine("This meta-package also contains the following interfaces:");
            sb.AppendLine();
        }

        var groupedInterfaces = result.Interfaces
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
