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

        if (!result.IsMetaPackage && result.Classes.Count == 0)
        {
            sb.AppendLine("No public classes found in this package.");
            return sb.ToString();
        }

        var prefix = result.PackageId + ".";

        foreach (var cls in result.Classes.OrderBy(c => c.FullName))
        {
            var formattedName = cls.FullName.FormatGenericTypeName();
            if (formattedName.StartsWith(prefix))
                formattedName = formattedName.Substring(prefix.Length);

            sb.Append("public ");
            if (cls.IsStatic)
                sb.Append("static ");
            else if (cls.IsAbstract)
                sb.Append("abstract ");
            else if (cls.IsSealed)
                sb.Append("sealed ");

            sb.AppendLine($"class {formattedName}");
        }

        return sb.ToString();
    }
}
