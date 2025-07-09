using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class InterfaceListResult : PackageResultBase
{
    public List<InterfaceInfo> Interfaces { get; set; } = [];
}
