using System;

using ModelContextProtocol;

namespace NuGetMcpServer.Extensions;

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

    public void ReportMessage(string message)
    {
        if (_disposed || _progress == null)
            return;

        if (_currentProgress < 99)
            _currentProgress++;

        _progress.Report(new ProgressNotificationValue
        {
            Progress = _currentProgress,
            Total = 100,
            Message = message
        });
    }

    public void Dispose()
    {
        if (_disposed || _progress == null)
            return;

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
