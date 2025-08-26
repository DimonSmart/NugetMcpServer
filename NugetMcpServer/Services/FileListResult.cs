using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class FileListResult : PackageResultBase
{
    public List<string> Files { get; set; } = [];
}

