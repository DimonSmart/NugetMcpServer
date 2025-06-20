namespace NuGetMcpServer.Extensions;

public interface IProgressNotifier
{
    void ReportMessage(string message);
}
