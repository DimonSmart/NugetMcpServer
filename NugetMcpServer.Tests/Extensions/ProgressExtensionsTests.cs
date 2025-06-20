using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using NuGetMcpServer.Extensions;
using NugetMcpServer.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Extensions;

public class ProgressExtensionsTests : TestBase
{
    private readonly TestProgress _progress;

    public ProgressExtensionsTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _progress = new TestProgress();
    }

    [Fact]
    public void ReportMessage_WithMessage_SetsCorrectValues()
    {
        // Act
        _progress.ReportMessage("Test operation");

        // Assert
        Assert.Single(_progress.Reports);
        var report = _progress.Reports[0];
        Assert.Equal(50, report.Progress);
        Assert.Equal(100, report.Total);
        Assert.Equal("Test operation", report.Message);
    }

    [Fact]
    public void ReportMessage_WithMessageAndPercentage_SetsCorrectValues()
    {
        // Act
        _progress.ReportMessage("Test operation", 75);

        // Assert
        Assert.Single(_progress.Reports);
        var report = _progress.Reports[0];
        Assert.Equal(75, report.Progress);
        Assert.Equal(100, report.Total);
        Assert.Equal("Test operation", report.Message);
    }

    [Fact]
    public void ReportStep_WithSteps_CalculatesCorrectPercentage()
    {
        // Act
        _progress.ReportStep("Step 1", 0, 4);
        _progress.ReportStep("Step 2", 1, 4);
        _progress.ReportStep("Step 3", 2, 4);
        _progress.ReportStep("Step 4", 3, 4);

        // Assert
        Assert.Equal(4, _progress.Reports.Count);
        Assert.Equal(0, _progress.Reports[0].Progress);
        Assert.Equal(25, _progress.Reports[1].Progress);
        Assert.Equal(50, _progress.Reports[2].Progress);
        Assert.Equal(75, _progress.Reports[3].Progress);
    }

    [Fact]
    public void ReportComplete_SetsCorrectValues()
    {
        // Act
        _progress.ReportComplete("Operation finished");

        // Assert
        Assert.Single(_progress.Reports);
        var report = _progress.Reports[0];
        Assert.Equal(100, report.Progress);
        Assert.Equal(100, report.Total);
        Assert.Equal("Operation finished", report.Message);
    }

    [Fact]
    public void ReportStart_SetsCorrectValues()
    {
        // Act
        _progress.ReportStart("Operation starting");

        // Assert
        Assert.Single(_progress.Reports);
        var report = _progress.Reports[0];
        Assert.Equal(0, report.Progress);
        Assert.Equal(100, report.Total);
        Assert.Equal("Operation starting", report.Message);
    }

    [Fact]
    public void Extensions_WithNullProgress_DoNotThrow()
    {
        // Arrange
        IProgress<ProgressNotificationValue>? nullProgress = null;

        // Act & Assert - should not throw
        nullProgress.ReportMessage("Test");
        nullProgress.ReportMessage("Test", 50);
        nullProgress.ReportStep("Test", 1, 4);
        nullProgress.ReportComplete("Test");
        nullProgress.ReportStart("Test");
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
