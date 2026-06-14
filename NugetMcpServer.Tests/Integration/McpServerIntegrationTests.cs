using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using NuGetMcpServer.Tests.Helpers;
using Xunit;

namespace NuGetMcpServer.Tests.Integration;

public class McpServerIntegrationTests(ITestOutputHelper testOutput) : TestBase(testOutput)
{
    [Fact]
    public async Task StdioTransport_CanInitializeListToolsAndCallTool()
    {
        var serverDll = FindServerDll();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var stderrLines = new List<string>();
        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = "NugetMcpServer integration test",
                Command = "dotnet",
                Arguments = [serverDll],
                WorkingDirectory = Path.GetDirectoryName(serverDll),
                StandardErrorLines = line => stderrLines.Add(line)
            },
            NullLoggerFactory.Instance);

        await using var client = await McpClient.CreateAsync(
            transport,
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: timeout.Token);

        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Contains(tools, tool => tool.Name == "list_interfaces");
        Assert.Contains(tools, tool => tool.Name == "get_current_time");

        var result = await client.CallToolAsync(
            "get_current_time",
            arguments: new Dictionary<string, object?>(),
            cancellationToken: timeout.Token);

        Assert.True(result.IsError != true);
        Assert.NotEmpty(result.Content);

        foreach (var line in stderrLines)
        {
            TestOutput.WriteLine($"SERVER STDERR: {line}");
        }
    }

    private static string FindServerDll()
    {
        return BuildOutputPaths.FindProjectAssembly("NugetMcpServer", "NugetMcpServer.dll");
    }
}
