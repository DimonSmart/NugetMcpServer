using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace NuGetMcpServer.Extensions;

public static class ExceptionHandlingExtensions
{
    public static async Task<T> ExecuteWithLoggingAsync<T>(Func<Task<T>> action, ILogger logger, string errorMessage, bool rethrow = true)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, errorMessage);
            if (rethrow)
            {
                throw;
            }

            return default!;
        }
    }
}
