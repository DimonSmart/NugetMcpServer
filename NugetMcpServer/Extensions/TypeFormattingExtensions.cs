using System;

namespace NuGetMcpServer.Extensions;

public static class TypeFormattingExtensions
{
    public static string FormatGenericTypeName(this string typeName)
    {
        var tickIndex = typeName.IndexOf('`', StringComparison.Ordinal);
        if (tickIndex < 0)
        {
            return typeName;
        }

        return typeName[..tickIndex];
    }

    public static string FormatFullGenericTypeName(this string typeName)
    {
        return typeName.Replace('+', '.').FormatGenericTypeName();
    }
}
