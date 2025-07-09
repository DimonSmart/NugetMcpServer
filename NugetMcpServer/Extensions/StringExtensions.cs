using System;

namespace NuGetMcpServer.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrEmptyOrNullString(this string? value) => string.IsNullOrEmpty(value) || value.EqualsIgnoreCase("null");

    public static bool EqualsIgnoreCase(this string? value, string compareTo) => value?.Equals(compareTo, StringComparison.OrdinalIgnoreCase) == true;
}
