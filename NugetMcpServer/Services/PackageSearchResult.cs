using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class PackageSearchResult
{
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public IReadOnlyCollection<PackageInfo> Packages { get; set; } = [];
}
