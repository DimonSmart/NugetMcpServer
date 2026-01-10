using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NuGetMcpServer.Configuration;
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

        var nugetSourceOptions = BuildNuGetSourceOptions(args, builder.Configuration);

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(nugetSourceOptions);
        builder.Services.AddSingleton<MetaPackageDetector>();
        builder.Services.AddSingleton<NuGetPackageService>();
        builder.Services.AddSingleton<PackageSearchService>();
        builder.Services.AddSingleton<ArchiveProcessingService>();
        builder.Services.AddSingleton<InterfaceFormattingService>();
        builder.Services.AddSingleton<EnumFormattingService>();
        builder.Services.AddSingleton<ClassFormattingService>();
        builder.Services.AddSingleton<DocumentationProvider>();
        builder.Services.AddSingleton<PackageComparisonService>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ListInterfacesTool).Assembly);

        await builder.Build().RunAsync();
        return 0;
    }

    private static NuGetSourceOptions BuildNuGetSourceOptions(string[] args, IConfiguration configuration)
    {
        var options = new NuGetSourceOptions();
        configuration.GetSection("NuGet").Bind(options);

        var envConfigPath = Environment.GetEnvironmentVariable("NUGET_CONFIG");
        if (!string.IsNullOrWhiteSpace(envConfigPath))
        {
            options.ConfigPath = envConfigPath;
        }

        var envSources = Environment.GetEnvironmentVariable("NUGET_SOURCES");
        if (!string.IsNullOrWhiteSpace(envSources))
        {
            options.Sources = SplitSources(envSources);
        }

        var parsed = ParseCommandLineSources(args);
        if (!string.IsNullOrWhiteSpace(parsed.ConfigPath))
        {
            options.ConfigPath = parsed.ConfigPath;
        }

        if (parsed.Sources.Count > 0)
        {
            options.Sources = parsed.Sources;
        }

        options.Sources = options.Sources
            .Select(source => source.Trim())
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(options.ConfigPath))
        {
            options.ConfigPath = NormalizeConfigPath(options.ConfigPath);
        }

        return options;
    }

    private static (string? ConfigPath, List<string> Sources) ParseCommandLineSources(string[] args)
    {
        var sources = new List<string>();
        string? configPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.Equals("--source", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    sources.Add(args[++i]);
                }
                continue;
            }

            if (arg.StartsWith("--source=", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(arg.Substring("--source=".Length));
                continue;
            }

            if (arg.Equals("--sources", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    sources.AddRange(SplitSources(args[++i]));
                }
                continue;
            }

            if (arg.StartsWith("--sources=", StringComparison.OrdinalIgnoreCase))
            {
                sources.AddRange(SplitSources(arg.Substring("--sources=".Length)));
                continue;
            }

            if (arg.Equals("--nuget-config", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--nugetconfig", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    configPath = args[++i];
                }
                continue;
            }

            if (arg.StartsWith("--nuget-config=", StringComparison.OrdinalIgnoreCase))
            {
                configPath = arg.Substring("--nuget-config=".Length);
                continue;
            }

            if (arg.StartsWith("--nugetconfig=", StringComparison.OrdinalIgnoreCase))
            {
                configPath = arg.Substring("--nugetconfig=".Length);
            }
        }

        return (configPath, sources);
    }

    private static List<string> SplitSources(string sources)
    {
        return sources
            .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(source => source.Trim())
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .ToList();
    }

    private static string NormalizeConfigPath(string configPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(configPath.Trim());
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), expanded));
    }
}
