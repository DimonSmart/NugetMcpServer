using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public abstract class PackageResultBase
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsMetaPackage { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<PackageDependency> Dependencies { get; set; } = [];
}
