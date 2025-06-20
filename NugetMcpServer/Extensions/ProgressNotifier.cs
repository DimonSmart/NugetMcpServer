using System;

using ModelContextProtocol;

namespace NuGetMcpServer.Extensions;

/// <summary>
/// Auto-incrementing progress notifier with using pattern support
/// </summary>
public sealed class ProgressNotifier : IDisposable, IProgressNotifier
{
    public sealed class NullProgressNotifier : IProgressNotifier
    {
        public void ReportMessage(string message)
        {
        }
    }

    public static IProgressNotifier VoidProgressNotifier = new NullProgressNotifier();

    private readonly IProgress<ProgressNotificationValue>? _progress;
    private int _currentProgress = 0;
    private bool _disposed = false;

    public ProgressNotifier(IProgress<ProgressNotificationValue>? progress)
    {
        _progress = progress;
    }

    /// <summary>
    /// Reports progress with auto-incrementing percentage (1-99)
    /// </summary>
    /// <param name="message">Operation description</param>
    public void ReportMessage(string message)
    {
        if (_disposed || _progress == null) return;

        if (_currentProgress < 99)
        {
            _currentProgress++;
        }

        _progress.Report(new ProgressNotificationValue
        {
            Progress = _currentProgress,
            Total = 100,
            Message = message
        });
    }

    /// <summary>
    /// Reports completion (100%) and disposes the notifier
    /// </summary>
    public void Dispose()
    {
        if (_disposed || _progress == null) return;

        _progress.Report(new ProgressNotificationValue
        {
            Progress = 100,
            Total = 100,
            Message = "Operation completed"
        });

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
