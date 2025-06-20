using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuGetMcpServer.Services;

namespace NuGetMcpServer.Common;

public abstract class McpToolBase<T>(ILogger<T> logger, NuGetPackageService packageService) where T : class
{
    protected readonly ILogger<T> Logger = logger;
    protected readonly NuGetPackageService PackageService = packageService;

    protected async Task<Assembly?> LoadAssemblyFromEntryAsync(ZipArchiveEntry entry)
    {
        using var entryStream = entry.Open();
        using var ms = new MemoryStream();
        await entryStream.CopyToAsync(ms);

        var assemblyData = ms.ToArray();
        return PackageService.LoadAssemblyFromMemory(assemblyData);
    }
}
