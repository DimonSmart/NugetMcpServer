using System.Text;

using NuGetMcpServer.Services;

namespace NuGetMcpServer.Common;

public static class MetaPackageHelper
{
    public static string CreateMetaPackageWarning(PackageInfo packageInfo, string packageId, string version)
    {
        if (!packageInfo.IsMetaPackage)
            return string.Empty;

        var warning = new StringBuilder();
        warning.AppendLine($"⚠️  META-PACKAGE: {packageId} v{version}");
        warning.AppendLine("This package groups other related packages together and may not contain actual implementation code.");

        if (packageInfo.Dependencies.Count > 0)
        {
            warning.AppendLine("Dependencies:");
            foreach (var dependency in packageInfo.Dependencies)
            {
                warning.AppendLine($"  • {dependency.Id} ({dependency.Version})");
            }
            warning.AppendLine("💡 To see actual implementations, analyze one of the dependency packages listed above.");
        }

        warning.AppendLine();
        warning.AppendLine(new string('-', 60));
        warning.AppendLine();

        return warning.ToString();
    }
}
