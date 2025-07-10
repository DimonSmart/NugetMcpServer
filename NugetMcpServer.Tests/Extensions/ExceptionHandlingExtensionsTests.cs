using Microsoft.Extensions.Logging;

using Moq;

using NuGetMcpServer.Extensions;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Extensions;

public class ExceptionHandlingExtensionsTests
{
    private readonly Mock<ILogger> _mockLogger = new();

    [Fact]
    public async Task ExecuteWithLoggingAsync_WhenActionSucceeds_ReturnsResult()
    {
        // Arrange
        string expected = "Success";

        // Act
        string result = await ExceptionHandlingExtensions.ExecuteWithLoggingAsync(
            () => Task.FromResult(expected),
            _mockLogger.Object,
            "Error message");

        // Assert
        Assert.Equal(expected, result);
        // Logger shouldn't be called for successful execution
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteWithLoggingAsync_WhenActionThrows_LogsErrorAndRethrows()
    {
        // Arrange
        InvalidOperationException exception = new InvalidOperationException("Test exception");
        string errorMessage = "Error occurred";

        // Act & Assert
        InvalidOperationException thrownException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ExceptionHandlingExtensions.ExecuteWithLoggingAsync<string>(
                () => Task.FromException<string>(exception),
                _mockLogger.Object,
                errorMessage));

        // Verify that logging occurred with the correct level and exception
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
