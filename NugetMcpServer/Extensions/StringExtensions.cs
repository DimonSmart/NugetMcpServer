using System;

namespace NuGetMcpServer.Extensions;

public static class StringExtensions
{
    // Checks if the string is null, empty or equals to "null" (case insensitive)
    public static bool IsNullOrEmptyOrNullString(this string? value) => string.IsNullOrEmpty(value) || value.EqualsIgnoreCase("null");

    public static bool EqualsIgnoreCase(this string? value, string compareTo) => value?.Equals(compareTo, StringComparison.OrdinalIgnoreCase) == true;
}
