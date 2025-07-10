using System.Collections.Generic;

using NuGetMcpServer.Models;

namespace NuGetMcpServer.Services;

public class PackageInfo : PackageResultBase
{
    public long DownloadCount { get; set; }
    public string? ProjectUrl { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Authors { get; set; }
    public List<string> FoundByKeywords { get; set; } = [];
    public string? LicenseUrl { get; set; }
}
