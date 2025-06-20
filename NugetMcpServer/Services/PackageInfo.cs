using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class PackageInfo
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long DownloadCount { get; set; }
    public string? ProjectUrl { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Authors { get; set; }
    public List<string> FoundByKeywords { get; set; } = [];
}
