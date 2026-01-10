using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using MsLogger = Microsoft.Extensions.Logging.ILogger;
using NuGetLogger = NuGet.Common.ILogger;
using NuGetLogLevel = NuGet.Common.LogLevel;

namespace NuGetMcpServer.Services;

public sealed class NuGetLoggerAdapter(MsLogger logger) : NuGetLogger
{
    private readonly MsLogger _logger = logger;

    public void LogDebug(string data) => _logger.LogDebug(data);

    public void LogVerbose(string data) => _logger.LogTrace(data);

    public void LogInformation(string data) => _logger.LogInformation(data);

    public void LogMinimal(string data) => _logger.LogInformation(data);

    public void LogWarning(string data) => _logger.LogWarning(data);

    public void LogError(string data) => _logger.LogError(data);

    public void LogInformationSummary(string data) => _logger.LogInformation(data);

    public void Log(NuGetLogLevel level, string data) => LogMessage(level, data);

    public Task LogAsync(NuGetLogLevel level, string data)
    {
        LogMessage(level, data);
        return Task.CompletedTask;
    }

    public void Log(ILogMessage message)
    {
        if (message == null)
        {
            return;
        }

        LogMessage(message.Level, message.Message);
    }

    public Task LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }

    private void LogMessage(NuGetLogLevel level, string data)
    {
        switch (level)
        {
            case NuGetLogLevel.Debug:
                LogDebug(data);
                break;
            case NuGetLogLevel.Verbose:
                LogVerbose(data);
                break;
            case NuGetLogLevel.Information:
                LogInformation(data);
                break;
            case NuGetLogLevel.Minimal:
                LogMinimal(data);
                break;
            case NuGetLogLevel.Warning:
                LogWarning(data);
                break;
            case NuGetLogLevel.Error:
                LogError(data);
                break;
            default:
                LogInformation(data);
                break;
        }
    }
}
