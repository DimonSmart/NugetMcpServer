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
            {
                return mappedName;
            }

            if (type.IsGenericType)
            {
                var genericTypeName = type.Name;
                var tickIndex = genericTypeName.IndexOf('`');

                if (tickIndex > 0)
                {
                    genericTypeName = genericTypeName.Substring(0, tickIndex);
                }

                var genericArgs = type.GetGenericArguments();
                return $"{genericTypeName}<{string.Join(", ", genericArgs.Select(FormatCSharpTypeName))}>";
            }

            return type.Name;
        }
        public static string FormatGenericTypeName(this string typeName)
        {
            var tickIndex = typeName.IndexOf('`');
            if (tickIndex <= 0)
            {
                return typeName;
            }

            var baseName = typeName.Substring(0, tickIndex);
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
