using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NuGetMcpServer.Services;
using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Helpers;

public abstract class TestBase(ITestOutputHelper testOutput)
{
    protected readonly ITestOutputHelper TestOutput = testOutput;
    protected readonly HttpClient HttpClient = new();

    protected MetaPackageDetector CreateMetaPackageDetector()
    {
        return new MetaPackageDetector(NullLogger<MetaPackageDetector>.Instance);
    }

    protected NuGetPackageService CreateNuGetPackageService()
    {
        var metaPackageDetector = CreateMetaPackageDetector();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new NuGetPackageService(NullLogger<NuGetPackageService>.Instance, HttpClient, metaPackageDetector, cache);
    }

    protected ArchiveProcessingService CreateArchiveProcessingService()
    {
        var packageService = CreateNuGetPackageService();
        return new ArchiveProcessingService(NullLogger<ArchiveProcessingService>.Instance, packageService);
    }


}
