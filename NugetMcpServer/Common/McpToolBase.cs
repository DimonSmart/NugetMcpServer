using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuGet.Packaging;

using NuGetMcpServer.Services;

namespace NuGetMcpServer.Common;

public abstract class McpToolBase<T>(ILogger<T> logger, NuGetPackageService packageService) where T : class
{
    protected readonly ILogger<T> Logger = logger;
    protected readonly NuGetPackageService PackageService = packageService;
}
