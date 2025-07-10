using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuGet.Packaging;

using NuGetMcpServer.Services;

namespace NuGetMcpServer.Common;

public abstract class McpToolBase<T>(ILogger<T> logger, NuGetPackageService packageService) where T : class
{
    protected readonly ILogger<T> Logger = logger;
    protected readonly NuGetPackageService PackageService = packageService;

    protected async Task<(Assembly? assembly, Type[] types)> LoadAssemblyFromFileAsync(PackageArchiveReader packageReader, string filePath)
    {
        using var fileStream = packageReader.GetStream(filePath);
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);

        var assemblyData = ms.ToArray();
        return PackageService.LoadAssemblyFromMemoryWithTypes(assemblyData);
    }
}
