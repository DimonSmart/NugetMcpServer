using System;
using System.Threading.Tasks;
using ModelContextProtocol;
using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Examples;

/// <summary>
/// Демонстрация использования упрощённых методов для работы с прогрессом
/// </summary>
public class ProgressExample
{
    /// <summary>
    /// Пример старого способа с длинными вызовами
    /// </summary>
    public async Task OldWayExample(IProgress<ProgressNotificationValue>? progress)
    {
        // Старый способ - много кода для простого сообщения
        progress?.Report(new ProgressNotificationValue() 
        { 
            Progress = 0, 
            Total = 100, 
            Message = "Starting operation" 
        });

        await Task.Delay(1000);

        progress?.Report(new ProgressNotificationValue() 
        { 
            Progress = 50, 
            Total = 100, 
            Message = "Processing data" 
        });

        await Task.Delay(1000);

        progress?.Report(new ProgressNotificationValue() 
        { 
            Progress = 100, 
            Total = 100, 
            Message = "Operation completed" 
        });
    }

    /// <summary>
    /// Пример нового способа с ProgressNotifier и using
    /// </summary>
    public async Task NewWayExample(IProgress<ProgressNotificationValue>? progress)
    {
        // Новый способ - автоматическое управление прогрессом
        using var progressNotifier = new ProgressNotifier(progress);
        
        progressNotifier.ReportMessage("Starting operation");
        await Task.Delay(1000);

        progressNotifier.ReportMessage("Processing data");
        await Task.Delay(1000);

        progressNotifier.ReportMessage("Finalizing");
        // 100% автоматически отправится при выходе из using
    }

    /// <summary>
    /// Пример с множественными шагами - прогресс автоматически увеличивается
    /// </summary>
    public async Task MultiStepExample(IProgress<ProgressNotificationValue>? progress)
    {
        using var progressNotifier = new ProgressNotifier(progress);
        
        progressNotifier.ReportMessage("Validating input");     // 1%
        await Task.Delay(200);
        
        progressNotifier.ReportMessage("Downloading package");  // 2%
        await Task.Delay(500);
        
        progressNotifier.ReportMessage("Extracting files");     // 3%
        await Task.Delay(300);
        
        progressNotifier.ReportMessage("Processing assemblies"); // 4%
        await Task.Delay(400);
        
        progressNotifier.ReportMessage("Generating results");   // 5%
        await Task.Delay(200);
        
        // 100% отправится автоматически в Dispose
    }

    /// <summary>
    /// Пример старого способа с расширениями
    /// </summary>
    public async Task ExtensionsExample(IProgress<ProgressNotificationValue>? progress)
    {
        // Способ с расширениями - нужно указывать проценты
        progress?.ReportStart("Starting operation");

        await Task.Delay(1000);

        progress?.ReportMessage("Processing data", 50);

        await Task.Delay(1000);

        progress?.ReportComplete("Operation completed");
    }
}
