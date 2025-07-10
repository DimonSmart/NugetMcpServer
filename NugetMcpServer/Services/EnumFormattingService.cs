using System;
using System.Text;

namespace NuGetMcpServer.Services;

public class EnumFormattingService
{
    public string FormatEnumDefinition(Type enumType, string? assemblyName = null, string? packageName = null)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"Type {enumType.Name} is not an enum", nameof(enumType));
        }

        var sb = new StringBuilder();
        
        if (assemblyName != null)
        {
            var header = packageName != null 
                ? $"/* C# ENUM FROM {assemblyName} (Package: {packageName}) */"
                : $"/* C# ENUM FROM {assemblyName} */";
            sb.AppendLine(header);
        }
        
        var underlyingType = Enum.GetUnderlyingType(enumType);
        var underlyingTypeName = TypeFormattingHelpers.FormatTypeName(underlyingType);

        sb.Append($"public enum {enumType.Name}");
        if (underlyingType != typeof(int))
        {
            sb.Append($" : {underlyingTypeName}");
        }

        sb.AppendLine().AppendLine("{");

        var values = Enum.GetValues(enumType);
        var names = Enum.GetNames(enumType);
        var lastIndex = names.Length - 1;

        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var value = Convert.ChangeType(values.GetValue(i), underlyingType);

            var valueSuffix = underlyingType == typeof(ulong) ? "UL" :
                                underlyingType == typeof(long) ? "L" : underlyingType == typeof(uint) ? "U" :
                                string.Empty;

            sb.Append($"    {name} = {value}{valueSuffix}");

            if (i < lastIndex)
            {
                sb.AppendLine(",");
            }
            else
            {
                sb.AppendLine();
            }
        }
        sb.AppendLine("}");
        return sb.ToString();
    }
}
