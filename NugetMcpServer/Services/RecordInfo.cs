namespace NuGetMcpServer.Services;

public class RecordInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;
    public bool IsStruct { get; set; }
}
