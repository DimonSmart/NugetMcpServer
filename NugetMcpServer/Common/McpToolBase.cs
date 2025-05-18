using Microsoft.Extensions.Logging;
using NuGetMcpServer.Services;

namespace NuGetMcpServer.Common;

/// <summary>
/// Base class for MCP tools providing common functionality
/// </summary>
public abstract class McpToolBase<T>(ILogger<T> logger, NuGetPackageService packageService) where T : class
{
    protected readonly ILogger<T> Logger = logger;
    protected readonly NuGetPackageService PackageService = packageService;
}
