using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetMcpServer.Extensions
{
    public static class TypeFormattingExtensions
    {
        private static readonly Dictionary<string, string> PrimitiveTypeMap = new()
        {
            { typeof(int).FullName!, "int" },
            { typeof(string).FullName!, "string" },
            { typeof(bool).FullName!, "bool" },
            { typeof(double).FullName!, "double" },
            { typeof(float).FullName!, "float" },
            { typeof(long).FullName!, "long" },
            { typeof(short).FullName!, "short" },
            { typeof(byte).FullName!, "byte" },
            { typeof(char).FullName!, "char" },
            { typeof(object).FullName!, "object" },
            { typeof(decimal).FullName!, "decimal" },
            { typeof(void).FullName!, "void" },
            { typeof(ulong).FullName!, "ulong" },
            { typeof(uint).FullName!, "uint" },
            { typeof(ushort).FullName!, "ushort" },
            { typeof(sbyte).FullName!, "sbyte" }
        };

        public static string FormatCSharpTypeName(this Type type)
        {
            if (PrimitiveTypeMap.TryGetValue(type.FullName ?? type.Name, out var mappedName))
                return mappedName;

            if (type.IsGenericParameter)
                return type.Name;

            static string GetNestedName(Type t)
            {
                var name = t.Name;
                var tickIndex = name.IndexOf('`');
                if (tickIndex > 0)
                    name = name.Substring(0, tickIndex);
                name = name.Replace('+', '.');
                if (t.IsNested && t.DeclaringType != null)
                    return $"{GetNestedName(t.DeclaringType)}.{name}";
                return name;
            }

            try
            {
                var resultName = GetNestedName(type);

                if (type.IsGenericType)
                {
                    var genericArgs = type.GetGenericArguments();
                    resultName += $"<{string.Join(", ", genericArgs.Select(FormatCSharpTypeName))}>";
                }

                return resultName;
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is TypeLoadException)
            {
                // Fall back to using the raw name when dependencies are missing
                return (type.FullName ?? type.Name).FormatFullGenericTypeName();
            }
        }
        public static string FormatGenericTypeName(this string typeName)
        {
            var tickIndex = typeName.IndexOf('`');
            if (tickIndex <= 0)
            {
                return typeName.Replace('+', '.');
            }

            var baseName = typeName.Substring(0, tickIndex).Replace('+', '.');
            var numGenericArgs = int.Parse(typeName.Substring(tickIndex + 1));

            var genericArgs = string.Join(", ", Enumerable.Range(0, numGenericArgs).Select(GetGenericParamName));

            return $"{baseName}<{genericArgs}>";
        }

        public static string FormatFullGenericTypeName(this string fullTypeName)
        {
            var lastDot = fullTypeName.LastIndexOf('.');
            if (lastDot <= 0)
            {
                return fullTypeName.FormatGenericTypeName();
            }

            var ns = fullTypeName.Substring(0, lastDot + 1);
            var name = fullTypeName.Substring(lastDot + 1);

            return $"{ns}{name.FormatGenericTypeName()}";
        }

        private static string GetGenericParamName(int index)
        {
            return index < 26
                ? ((char)('T' + index)).ToString()
                : $"T{index}";
        }
    }
}
