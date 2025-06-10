using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

/// <summary>
/// Response model for class listing including package version information
/// </summary>
public class ClassListResult
{
    /// <summary>
    /// NuGet package ID
    /// </summary>
    public string PackageId { get; set; } = string.Empty;
    /// <summary>
    /// Package version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// List of classes found in the package
    /// </summary>
    public List<ClassInfo> Classes { get; set; } = [];

    /// <summary>
    /// Returns a formatted string representation of the class list
    /// </summary>
    /// <returns>Formatted list of classes</returns>
    public string ToFormattedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/* CLASSES FROM {PackageId} v{Version} */");
        sb.AppendLine();

        // Group classes by assembly name
        var groupedClasses = Classes
            .GroupBy(c => c.AssemblyName)
            .OrderBy(g => g.Key);

        foreach (var group in groupedClasses)
        {
            // Add assembly name as a sub-header
            sb.AppendLine($"## {group.Key}");

            // List classes from this assembly without repeating the DLL name
            foreach (var cls in group.OrderBy(c => c.FullName))
            {
                var formattedName = cls.GetFormattedFullName();
                var modifiers = new List<string>();

                if (cls.IsStatic) modifiers.Add("static");
                if (cls.IsAbstract) modifiers.Add("abstract");
                if (cls.IsSealed) modifiers.Add("sealed");

                var modifierString = modifiers.Count > 0 ? $" ({string.Join(", ", modifiers)})" : "";
                sb.AppendLine($"- {formattedName}{modifierString}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
