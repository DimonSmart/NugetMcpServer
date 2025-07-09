using System.Text;

namespace NuGetMcpServer.Services.Formatters;

public static class MetaPackageResultFormatter
{
    public static string Format(this MetaPackageResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/* META-PACKAGE: {result.PackageId} v{result.Version} */");
        sb.AppendLine();
        sb.AppendLine("This is a meta-package that groups other related packages together.");
        sb.AppendLine("Meta-packages do not contain their own implementation but serve as convenient");
        sb.AppendLine("collections of dependencies. To see actual classes and interfaces, please");
        sb.AppendLine("analyze one of the following dependency packages:");
        sb.AppendLine();

        if (result.Dependencies.Count > 0)
        {
            sb.AppendLine("Dependencies:");
            foreach (var dependency in result.Dependencies)
            {
                sb.AppendLine($"  • {dependency.Id} ({dependency.Version})");
            }
        }
        else
        {
            sb.AppendLine("No dependencies found (this may indicate an empty meta-package).");
        }

        if (!string.IsNullOrEmpty(result.Description))
        {
            sb.AppendLine();
            sb.AppendLine($"Description: {result.Description}");
        }

        return sb.ToString();
    }
}
