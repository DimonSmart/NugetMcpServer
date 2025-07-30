using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Helpers;

// Logger implementation for tests that writes to XUnit test output
public class TestLogger<T>(ITestOutputHelper output) : ILogger<T>
{
    private readonly ITestOutputHelper _output = output;

    public record LogEntry(LogLevel Level, string Message, Exception? Exception);

    public List<LogEntry> Entries { get; } = new();

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
        var message = formatter(state, exception);
        Entries.Add(new LogEntry(logLevel, message, exception));

        _output.WriteLine($"[{logLevel}] {message}");

        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception.Message}");
            _output.WriteLine(exception.StackTrace);
        }
    }
}
