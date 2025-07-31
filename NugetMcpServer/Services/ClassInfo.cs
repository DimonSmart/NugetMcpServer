namespace NuGetMcpServer.Services;

public class ClassInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public bool IsRecord { get; set; }
    public bool IsStruct { get; set; }
}
