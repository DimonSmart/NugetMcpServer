using Microsoft.Extensions.Logging.Abstractions;

using NuGetMcpServer.Services;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Helpers;

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
        return new NuGetPackageService(NullLogger<NuGetPackageService>.Instance, HttpClient, metaPackageDetector);
    }

    protected ArchiveProcessingService CreateArchiveProcessingService()
    {
        var packageService = CreateNuGetPackageService();
        return new ArchiveProcessingService(NullLogger<ArchiveProcessingService>.Instance, packageService);
    }

    protected static async Task ExecuteWithCleanupAsync(Func<Task> operation, Action cleanup)
    {
        try
        {
            await operation();
        }
        finally
        {
            cleanup();
        }
    }

    protected static void ExecuteWithErrorHandling(Action action, Action<Exception>? exceptionHandler = null)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            exceptionHandler?.Invoke(ex);
        }
    }
}
