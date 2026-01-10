using System.Collections.Generic;

namespace NuGetMcpServer.Configuration;

public sealed class NuGetSourceOptions
{
    public List<string> Sources { get; set; } = [];
    public string? ConfigPath { get; set; }
}
