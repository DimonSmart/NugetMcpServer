using System;

namespace NuGetMcpServer.Models;

/// <summary>
/// Progress notification value for long-running operations
/// </summary>
public class ProgressNotificationValue
{
    /// <summary>
    /// Current progress percentage (0-100)
    /// </summary>
    public double Percentage { get; set; }

    /// <summary>
    /// Current operation message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Current step in the operation
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Total number of steps
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Additional details about the operation
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Creates a new progress notification
    /// </summary>
    public ProgressNotificationValue(double percentage, string message, int currentStep = 0, int totalSteps = 0, string? details = null)
    {
        Percentage = Math.Max(0, Math.Min(100, percentage));
        Message = message;
        CurrentStep = currentStep;
        TotalSteps = totalSteps;
        Details = details;
    }

    /// <summary>
    /// Creates a progress notification from step counts
    /// </summary>
    public static ProgressNotificationValue FromSteps(int currentStep, int totalSteps, string message, string? details = null)
    {
        var percentage = totalSteps > 0 ? (double)currentStep / totalSteps * 100.0 : 0;
        return new ProgressNotificationValue(percentage, message, currentStep, totalSteps, details);
    }
}
