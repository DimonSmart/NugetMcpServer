using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class TypeListResult : PackageResultBase
{
    public List<TypeInfo> Types { get; set; } = [];
    public int TotalCount { get; set; }
    public int ReturnedCount { get; set; }
    public bool IsPartial { get; set; }
}
