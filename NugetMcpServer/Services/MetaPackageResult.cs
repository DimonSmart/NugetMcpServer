using System.Text;

namespace NuGetMcpServer.Services;

public class MetaPackageResult : PackageResultBase
{
    public MetaPackageResult()
    {
        IsMetaPackage = true;
    }

    public override string ToFormattedString()
    {
        var result = new StringBuilder();
        result.AppendLine($"/* META-PACKAGE: {PackageId} v{Version} */");
        result.AppendLine();
        result.AppendLine("This is a meta-package that groups other related packages together.");
        result.AppendLine("Meta-packages do not contain their own implementation but serve as convenient");
        result.AppendLine("collections of dependencies. To see actual classes and interfaces, please");
        result.AppendLine("analyze one of the following dependency packages:");
        result.AppendLine();

        if (Dependencies.Count > 0)
        {
            result.AppendLine("Dependencies:");
            foreach (var dependency in Dependencies)
            {
                result.AppendLine($"  • {dependency.Id} ({dependency.Version})");
            }
        }
        else
        {
            result.AppendLine("No dependencies found (this may indicate an empty meta-package).");
        }

        if (!string.IsNullOrEmpty(Description))
        {
            result.AppendLine();
            result.AppendLine($"Description: {Description}");
        }

        return result.ToString();
    }
}
