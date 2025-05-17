using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace NuGetMcpServer.Extensions;

/// <summary>
/// Extension methods for exception handling
/// </summary>
public static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Executes an action with exception handling and logging
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="action">The function to execute</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="errorMessage">Error message to log</param>
    /// <param name="rethrow">Whether to rethrow the exception</param>
    /// <returns>Result of the action</returns>
    public static T ExecuteWithLogging<T>(
        Func<T> action,
        ILogger logger,
        string errorMessage,
        bool rethrow = true)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, errorMessage);
            if (rethrow)
                throw;

            // For non-void methods when not rethrowing
            return default!;
        }
    }

    /// <summary>
    /// Executes an async action with exception handling and logging
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="action">The async function to execute</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="errorMessage">Error message to log</param>
    /// <param name="rethrow">Whether to rethrow the exception</param>
    /// <returns>Result of the action</returns>
    public static async Task<T> ExecuteWithLoggingAsync<T>(
        Func<Task<T>> action,
        ILogger logger,
        string errorMessage,
        bool rethrow = true)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, errorMessage);
            if (rethrow)
                throw;

            // For non-void methods when not rethrowing
            return default!;
        }
    }
}
