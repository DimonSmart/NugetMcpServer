using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Helpers;

// Logger implementation for tests that writes to XUnit test output
public class TestLogger<T>(ITestOutputHelper output) : ILogger<T>
{
    private readonly ITestOutputHelper _output = output;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");

        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception.Message}");
            _output.WriteLine(exception.StackTrace);
        }
    }
}
