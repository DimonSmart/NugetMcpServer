using System.Collections.Generic;

using NuGetMcpServer.Models;

namespace NuGetMcpServer.Tools;

public class PackageAnalysisResult
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsMetaPackage { get; set; }
    public bool HasDependencyPackageType { get; set; }
    public List<PackageDependency> Dependencies { get; set; } = [];
    public List<string> LibFiles { get; set; } = [];
    public string Description { get; set; } = string.Empty;
}
