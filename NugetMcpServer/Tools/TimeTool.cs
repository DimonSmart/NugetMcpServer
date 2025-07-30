using System;
using System.ComponentModel;
using ModelContextProtocol.Server;
namespace NuGetMcpServer.Tools;

[McpServerToolType]
public static class TimeTool
{
    [McpServerTool]
    [Description("Returns the current server time in ISO 8601 format (YYYY-MM-DDThh:mm:ssZ).")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    public static string get_current_time()
    {
        return DateTime.UtcNow.ToString("o");
    }
}
