using Microsoft.Extensions.Logging;
using Moq;
using NuGetMcpServer.Extensions;
using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Extensions;

public class ExceptionHandlingExtensionsTests(ITestOutputHelper testOutput)
{
    private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

    [Fact]
    public void ExecuteWithLogging_WhenActionSucceeds_ReturnsResult()
    {
        // Arrange
        var expected = "Success";

        // Act
        var result = ExceptionHandlingExtensions.ExecuteWithLogging(
            () => expected,
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
    public void ExecuteWithLogging_WhenActionThrows_LogsErrorAndRethrows()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var errorMessage = "Error occurred";

        // Act & Assert
        var thrownException = Assert.Throws<InvalidOperationException>(() =>
            ExceptionHandlingExtensions.ExecuteWithLogging<string>(
                () => throw exception,
                _mockLogger.Object,
                errorMessage));

        // Verify the exception is the same one we threw
        Assert.Same(exception, thrownException);

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

    [Fact]
    public void ExecuteWithLogging_WhenActionThrowsAndNoRethrow_LogsErrorOnly()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var errorMessage = "Error occurred";

        // Act
        var result = ExceptionHandlingExtensions.ExecuteWithLogging<string>(
            () => throw exception,
            _mockLogger.Object,
            errorMessage,
            rethrow: false);

        // Assert
        Assert.Null(result);

        // Verify that logging occurred but no exception propagated
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteWithLoggingAsync_WhenActionSucceeds_ReturnsResult()
    {
        // Arrange
        var expected = "Success";

        // Act
        var result = await ExceptionHandlingExtensions.ExecuteWithLoggingAsync(
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
        var exception = new InvalidOperationException("Test exception");
        var errorMessage = "Error occurred";

        // Act & Assert
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
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
