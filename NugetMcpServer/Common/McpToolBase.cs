using Microsoft.Extensions.Logging;
using NuGetMcpServer.Services;

namespace NuGetMcpServer.Common;

/// <summary>
/// Base class for MCP tools providing common functionality
/// </summary>
public abstract class McpToolBase<T> where T : class
{
    protected readonly ILogger<T> Logger;
    protected readonly NuGetPackageService PackageService;

    protected McpToolBase(ILogger<T> logger, NuGetPackageService packageService)
    {
        Logger = logger;
        PackageService = packageService;
    }
}
