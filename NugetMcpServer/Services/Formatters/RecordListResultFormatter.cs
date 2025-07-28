using System.Linq;
using System.Text;
using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services.Formatters;

public static class RecordListResultFormatter
{
    public static string Format(this RecordListResult result)
    {
        var sb = new StringBuilder();
        sb.Append(result.GetMetaPackageWarningIfAny());

        if (result.ShouldShowMetaPackageWarningOnly(result.Records.Count))
            return sb.ToString();

        if (!result.IsMetaPackage)
        {
            sb.AppendLine($"Records from {result.PackageId} v{result.Version}");
            sb.AppendLine(new string('=', $"Records from {result.PackageId} v{result.Version}".Length));
            sb.AppendLine();
        }

        if (result.Records.Count == 0)
        {
            sb.AppendLine("No public records found in this package.");
            return sb.ToString();
        }

        if (result.IsMetaPackage && result.Records.Count > 0)
        {
            sb.AppendLine("This meta-package also contains the following records:");
            sb.AppendLine();
        }

        var grouped = result.Records.GroupBy(r => r.AssemblyName).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key}");
            foreach (var rec in group.OrderBy(r => r.FullName))
            {
                var formattedName = rec.FullName.FormatGenericTypeName();
                var suffix = rec.IsStruct ? " (struct)" : string.Empty;
                sb.AppendLine($"- {formattedName}{suffix}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
