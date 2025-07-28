using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class StructListResult : PackageResultBase
{
    public List<StructInfo> Structs { get; set; } = [];
}
