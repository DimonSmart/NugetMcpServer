namespace NuGetMcpServer.Services;

public class FileContentResult : PackageResultBase
{
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsBinary { get; set; }
}

