using System;
using System.Linq;

namespace NuGetMcpServer.Services;

internal static class ApiModelSearch
{
    public static ApiTypeModel? FindType(PackageApiModel api, string name, Func<ApiTypeModel, bool> predicate)
    {
        return api.Assemblies
            .SelectMany(static assembly => assembly.Types)
            .Where(predicate)
            .FirstOrDefault(type => Matches(type, name));
    }

    public static bool Matches(ApiTypeModel type, string query)
    {
        return MatchesName(type.Name, type.FullName, query);
    }

    private static bool MatchesName(string name, string fullName, string query)
    {
        if (string.Equals(name, query, StringComparison.Ordinal) ||
            string.Equals(fullName, query, StringComparison.Ordinal))
        {
            return true;
        }

        var simple = StripGenericArguments(name);
        var fullSimple = StripGenericArguments(fullName);
        if (string.Equals(simple, query, StringComparison.Ordinal) ||
            string.Equals(fullSimple, query, StringComparison.Ordinal))
        {
            return true;
        }

        var clrName = ToClrGenericName(name);
        var clrFullName = ToClrGenericName(fullName);
        return string.Equals(clrName, query, StringComparison.Ordinal) ||
               string.Equals(clrFullName, query, StringComparison.Ordinal);
    }

    private static string StripGenericArguments(string value)
    {
        var index = value.IndexOf('<', StringComparison.Ordinal);
        return index < 0 ? value : value[..index];
    }

    private static string ToClrGenericName(string value)
    {
        var start = value.IndexOf('<', StringComparison.Ordinal);
        if (start < 0)
        {
            return value;
        }

        var end = value.LastIndexOf('>');
        if (end < start)
        {
            return value;
        }

        var arity = value[(start + 1)..end].Split(',', StringSplitOptions.TrimEntries).Length;
        return $"{value[..start]}`{arity}";
    }
}
