using System;
using System.Collections.Generic;
using ModelContextProtocol;
using NuGetMcpServer.Extensions;
using NugetMcpServer.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Extensions;

public class ProgressNotifierTests : TestBase
{
    private readonly TestProgress _progress;

    public ProgressNotifierTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _progress = new TestProgress();
    }

    [Fact]
    public void ReportMessage_AutoIncrements_From1To99()
    {
        // Arrange
        using var notifier = new ProgressNotifier(_progress);

        // Act
        notifier.ReportMessage("First");
        notifier.ReportMessage("Second");
        notifier.ReportMessage("Third");

        // Assert
        Assert.Equal(3, _progress.Reports.Count);
        Assert.Equal(1, _progress.Reports[0].Progress);
        Assert.Equal(2, _progress.Reports[1].Progress);
        Assert.Equal(3, _progress.Reports[2].Progress);
    }

    [Fact]
    public void ReportMessage_StopsAt99_WhenCalledMany()
    {
        // Arrange
        using var notifier = new ProgressNotifier(_progress);

        // Act - call 101 times
        for (int i = 0; i < 101; i++)
        {
            notifier.ReportMessage($"Message {i}");
        }

        // Assert - should never exceed 99
        Assert.All(_progress.Reports, report => Assert.True(report.Progress <= 99));
        
        // Last few reports should be 99
        Assert.Equal(99, _progress.Reports[99].Progress);
        Assert.Equal(99, _progress.Reports[100].Progress);
    }

    [Fact]
    public void Dispose_Reports100Percent()
    {
        // Arrange & Act
        using (var notifier = new ProgressNotifier(_progress))
        {
            notifier.ReportMessage("Test");
        } // Dispose called here

        // Assert
        Assert.Equal(2, _progress.Reports.Count);
        Assert.Equal(1, _progress.Reports[0].Progress); // First ReportMessage
        Assert.Equal(100, _progress.Reports[1].Progress); // Dispose
        Assert.Equal("Operation completed", _progress.Reports[1].Message);
    }

    [Fact]
    public void WithNullProgress_DoesNotThrow()
    {
        // Arrange & Act & Assert - should not throw
        using var notifier = new ProgressNotifier(null);
        notifier.ReportMessage("Test");
    }

    [Fact]
    public void MultipleDispose_OnlyReports100Once()
    {
        // Arrange
        var notifier = new ProgressNotifier(_progress);
        notifier.ReportMessage("Test");

        // Act
        notifier.Dispose();
        notifier.Dispose(); // Second dispose

        // Assert
        Assert.Equal(2, _progress.Reports.Count);
        Assert.Equal(1, _progress.Reports[0].Progress);
        Assert.Equal(100, _progress.Reports[1].Progress);
    }

    [Fact]
    public void ReportMessage_AfterDispose_DoesNothing()
    {
        // Arrange
        var notifier = new ProgressNotifier(_progress);
        notifier.Dispose();

        // Act
        notifier.ReportMessage("Should not report");

        // Assert
        Assert.Single(_progress.Reports);
        Assert.Equal(100, _progress.Reports[0].Progress);
    }

    private class TestProgress : IProgress<ProgressNotificationValue>
    {
        public List<ProgressNotificationValue> Reports { get; } = new();

        public void Report(ProgressNotificationValue value)
        {
            Reports.Add(value);
        }
    }
}
