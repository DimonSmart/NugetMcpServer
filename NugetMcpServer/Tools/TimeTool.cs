using System;
using System.ComponentModel;

using ModelContextProtocol.Server;

namespace NugetMcpServer.Tools;

[McpServerToolType]
public static class TimeTool
{
    [McpServerTool]
    [Description("Returns the current server time in ISO 8601 format (YYYY-MM-DDThh:mm:ssZ).")]
    public static string GetCurrentTime()
    {
        // Returns current UTC time in "YYYY-MM-DDThh:mm:ssZ" format
        return DateTime.UtcNow.ToString("o");
    }
}
