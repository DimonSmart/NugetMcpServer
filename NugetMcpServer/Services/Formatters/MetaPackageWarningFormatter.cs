namespace NuGetMcpServer.Services.Formatters;

public static class MetaPackageWarningFormatter
{
    public static string GetMetaPackageWarningIfAny(this PackageResultBase result)
    {
        if (!result.IsMetaPackage) return string.Empty;

        var warning = $"⚠️  META-PACKAGE: {result.PackageId} v{result.Version}\n";
        warning += "This package groups other related packages together and may not contain actual implementation code.\n";

        if (result.Dependencies.Count > 0)
        {
            warning += "Dependencies:\n";
            foreach (var dependency in result.Dependencies)
            {
                warning += $"  • {dependency.Id} ({dependency.Version})\n";
            }
            warning += "💡 To see actual implementations, analyze one of the dependency packages listed above.\n";
        }

        return warning + "\n" + new string('-', 60) + "\n\n";
    }

    public static bool ShouldShowMetaPackageWarningOnly(this PackageResultBase result, int itemCount)
    {
        return result.IsMetaPackage && itemCount == 0;
    }
}
