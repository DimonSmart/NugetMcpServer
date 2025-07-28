using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public class RecordListResult : PackageResultBase
{
    public List<RecordInfo> Records { get; set; } = [];
}
