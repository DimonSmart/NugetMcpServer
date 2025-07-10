using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Any(a => a is "--version" or "-v"))
        {
            var asm = Assembly.GetExecutingAssembly();
            var version =
                asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "unknown";

            Console.WriteLine($"NuGetMcpServer {version}");
            return 0;
        }

        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<MetaPackageDetector>();
        builder.Services.AddSingleton<NuGetPackageService>();
        builder.Services.AddSingleton<PackageSearchService>();
        builder.Services.AddSingleton<ArchiveProcessingService>();
        builder.Services.AddSingleton<InterfaceFormattingService>();
        builder.Services.AddSingleton<EnumFormattingService>();
        builder.Services.AddSingleton<ClassFormattingService>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ListInterfacesTool).Assembly);

        await builder.Build().RunAsync();
        return 0;
    }
}
