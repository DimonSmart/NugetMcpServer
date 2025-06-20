using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Helpers;

public abstract class TestBase(ITestOutputHelper testOutput)
{
    protected readonly ITestOutputHelper TestOutput = testOutput;
    protected readonly HttpClient HttpClient = new();

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
