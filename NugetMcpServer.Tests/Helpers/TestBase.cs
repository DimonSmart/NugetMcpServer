using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NuGetMcpServer.Configuration;
using NuGetMcpServer.Services;
using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Helpers;

public abstract class TestBase(ITestOutputHelper testOutput)
{
    protected readonly ITestOutputHelper TestOutput = testOutput;

    protected MetaPackageDetector CreateMetaPackageDetector()
    {
        return new MetaPackageDetector(NullLogger<MetaPackageDetector>.Instance);
    }

    protected NuGetPackageService CreateNuGetPackageService()
    {
        var metaPackageDetector = CreateMetaPackageDetector();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new NuGetSourceOptions();
        return new NuGetPackageService(NullLogger<NuGetPackageService>.Instance, metaPackageDetector, cache, options);
    }

    protected ArchiveProcessingService CreateArchiveProcessingService()
    {
        var packageService = CreateNuGetPackageService();
        return new ArchiveProcessingService(NullLogger<ArchiveProcessingService>.Instance, packageService);
    }


}
