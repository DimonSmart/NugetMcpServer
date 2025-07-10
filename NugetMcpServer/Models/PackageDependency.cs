namespace NuGetMcpServer.Models;

public class PackageDependency
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? TargetFramework { get; set; }
}
