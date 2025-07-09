using System.Collections.Generic;
using System.Text;

namespace NuGetMcpServer.Services;

public abstract class PackageResultBase
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsMetaPackage { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<PackageDependency> Dependencies { get; set; } = [];

    /// <summary>
    /// Formats the result into a human-readable string
    /// </summary>
    public abstract string ToFormattedString();

    /// <summary>
    /// Gets the formatted header for meta-package information
    /// </summary>
    protected string GetMetaPackageHeader()
    {
        if (!IsMetaPackage) return string.Empty;

        var header = $"/* META-PACKAGE: {PackageId} v{Version} */\n\n";
        header += "This is a meta-package that groups other related packages together.\n";
        
        if (Dependencies.Count > 0)
        {
            header += "Dependencies:\n";
            foreach (var dependency in Dependencies)
            {
                header += $"  • {dependency.Id} ({dependency.Version})\n";
            }
            header += "\n";
        }

        return header;
    }

    /// <summary>
    /// Gets the recommendation text for meta-packages without own content
    /// </summary>
    protected string GetMetaPackageRecommendation()
    {
        return "To see actual classes and interfaces, please analyze one of the dependency packages listed above.\n\n";
    }

    /// <summary>
    /// Handles common meta-package formatting logic
    /// </summary>
    /// <param name="sb">StringBuilder to append to</param>
    /// <param name="itemCount">Number of items (classes/interfaces) in the package</param>
    /// <param name="itemTypePlural">Plural name of the item type (e.g., "classes", "interfaces")</param>
    /// <returns>True if processing should continue, false if result is complete</returns>
    protected bool HandleMetaPackageFormatting(StringBuilder sb, int itemCount, string itemTypePlural)
    {
        if (!IsMetaPackage) return true;

        sb.Append(GetMetaPackageHeader());
        
        if (itemCount == 0)
        {
            sb.Append(GetMetaPackageRecommendation());
            return false;
        }
        
        sb.AppendLine($"This meta-package also contains the following {itemTypePlural}:\n");
        return true;
    }
}
