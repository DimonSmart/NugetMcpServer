using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class ClassListResult : PackageResultBase
{
    public List<ClassInfo> Classes { get; set; } = [];
}
