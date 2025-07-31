using System.Linq;
using System.Text;
using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services.Formatters;

public static class TypeListResultFormatter
{
    public static string Format(this TypeListResult result)
    {
        var sb = new StringBuilder();
        sb.Append(result.GetMetaPackageWarningIfAny());

        if (result.ShouldShowMetaPackageWarningOnly(result.Types.Count))
            return sb.ToString();

        if (!result.IsMetaPackage && result.Types.Count == 0)
        {
            sb.AppendLine("No public classes, records or structs found in this package.");
            return sb.ToString();
        }

        var prefix = result.PackageId + ".";

        foreach (var type in result.Types.OrderBy(t => t.FullName))
        {
            var formattedName = type.FullName.FormatGenericTypeName();
            if (formattedName.StartsWith(prefix))
                formattedName = formattedName.Substring(prefix.Length);

            sb.Append("public ");
            var modifier = type.IsStatic
                ? "static "
                : type.IsAbstract
                    ? "abstract "
                    : type.IsSealed
                        ? "sealed "
                        : string.Empty;
            sb.Append(modifier);

            string keyword = type.Kind switch
            {
                TypeKind.RecordClass => "record",
                TypeKind.RecordStruct => "record struct",
                TypeKind.Struct => "struct",
                _ => "class"
            };

            sb.AppendLine($"{keyword} {formattedName}");
        }

        return sb.ToString();
    }
}
