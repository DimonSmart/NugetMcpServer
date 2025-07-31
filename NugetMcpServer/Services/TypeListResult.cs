using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class TypeListResult : PackageResultBase
{
    public List<TypeInfo> Types { get; set; } = [];
}
