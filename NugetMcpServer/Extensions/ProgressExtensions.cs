using System;
using ModelContextProtocol;

namespace NuGetMcpServer.Extensions;

public static class ProgressExtensions
{
    /// <summary>
    /// Reports progress with automatic percentage calculation based on operation step
    /// </summary>
    /// <param name="progress">The progress reporter</param>
    /// <param name="message">Operation description</param>
    /// <param name="currentStep">Current step number (0-based)</param>
    /// <param name="totalSteps">Total number of steps</param>
    public static void ReportStep(this IProgress<ProgressNotificationValue>? progress, string message, int currentStep, int totalSteps)
    {
        if (progress == null) return;
        
        var percentage = totalSteps > 0 ? (currentStep * 100) / totalSteps : 0;
        progress.Report(new ProgressNotificationValue 
        { 
            Progress = percentage, 
            Total = 100, 
            Message = message 
        });
    }    /// <summary>
    /// Reports progress with a simple message using default percentage
    /// </summary>
    /// <param name="progress">The progress reporter</param>
    /// <param name="message">Operation description</param>
    /// <param name="percentage">Progress percentage (0-100)</param>
    public static void ReportMessage(this IProgress<ProgressNotificationValue>? progress, string message, int percentage = 50)
    {
        if (progress == null) return;
        
        progress.Report(new ProgressNotificationValue 
        { 
            Progress = percentage, 
            Total = 100, 
            Message = message 
        });
    }

    /// <summary>
    /// Reports progress completion (100%)
    /// </summary>
    /// <param name="progress">The progress reporter</param>
    /// <param name="message">Completion message</param>
    public static void ReportComplete(this IProgress<ProgressNotificationValue>? progress, string message)
    {
        if (progress == null) return;
        
        progress.Report(new ProgressNotificationValue 
        { 
            Progress = 100, 
            Total = 100, 
            Message = message 
        });
    }

    /// <summary>
    /// Reports progress start (0%)
    /// </summary>
    /// <param name="progress">The progress reporter</param>
    /// <param name="message">Start message</param>
    public static void ReportStart(this IProgress<ProgressNotificationValue>? progress, string message)
    {
        if (progress == null) return;
        
        progress.Report(new ProgressNotificationValue 
        { 
            Progress = 0, 
            Total = 100, 
            Message = message 
        });
    }
}
