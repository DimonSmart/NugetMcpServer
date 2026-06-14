using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;
using Xunit;
using static NuGetMcpServer.Extensions.ProgressNotifier;

namespace NuGetMcpServer.Tests.Integration;

public class PopularNuGetPackagesMetadataSmokeTests : TestBase
{
    private static readonly string[] PopularPackages =
    [
        "Newtonsoft.Json",
        "System.Text.Json",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.Http",
        "Microsoft.EntityFrameworkCore",
        "Dapper",
        "Serilog",
        "AutoMapper",
        "FluentValidation",
        "Polly",
        "MediatR",
        "NodaTime",
        "CsvHelper",
        "RestSharp",
        "AngleSharp",
        "HtmlAgilityPack",
        "Humanizer",
        "Spectre.Console",
        "CommandLineParser",
        "Microsoft.Identity.Client",
        "Azure.Core",
        "Azure.Identity",
        "Grpc.Net.Client",
        "Google.Protobuf",
        "StackExchange.Redis",
        "Bogus",
        "BenchmarkDotNet",
        "xunit.v3",
        "Moq"
    ];

    private static readonly IReadOnlyDictionary<string, PackageSmokeExpectation> Expectations =
        new Dictionary<string, PackageSmokeExpectation>(StringComparer.OrdinalIgnoreCase)
        {
            ["Newtonsoft.Json"] = new()
            {
                PackageId = "Newtonsoft.Json",
                MinPublicTypes = 20,
                ExpectedTypeNames =
                [
                    "Newtonsoft.Json.JsonConvert",
                    "Newtonsoft.Json.JsonSerializer",
                    "Newtonsoft.Json.Linq.JObject"
                ]
            },
            ["Dapper"] = new()
            {
                PackageId = "Dapper",
                MinPublicTypes = 5,
                ExpectedTypeNames = ["Dapper.SqlMapper"]
            },
            ["Serilog"] = new()
            {
                PackageId = "Serilog",
                MinPublicTypes = 10,
                ExpectedTypeNames = ["Serilog.Log", "Serilog.ILogger"]
            }
        };

    private readonly NuGetPackageService _packageService;

    public PopularNuGetPackagesMetadataSmokeTests(ITestOutputHelper output) : base(output)
    {
        _packageService = CreateNuGetPackageService();
    }

    [Fact]
    [Trait("Category", "Exploratory")]
    [Trait("Category", "Manual")]
    public async Task PopularNuGetPackages_MetadataParser_IsProductionSmokeStable()
    {
        if (Environment.GetEnvironmentVariable("NUGET_MCP_RUN_EXPLORATORY_TESTS") != "1")
        {
            TestOutput.WriteLine("Skipping exploratory smoke test because NUGET_MCP_RUN_EXPLORATORY_TESTS is not 1.");
            return;
        }

        var archiveService = CreateArchiveProcessingService();
        var listTypesTool = new ListTypesTool(new TestLogger<ListTypesTool>(TestOutput), _packageService, archiveService);
        var listInterfacesTool = new ListInterfacesTool(new TestLogger<ListInterfacesTool>(TestOutput), _packageService, archiveService);
        var classDefinitionTool = new GetClassDefinitionTool(new TestLogger<GetClassDefinitionTool>(TestOutput), _packageService, new ApiDefinitionFormatter(), archiveService);
        var interfaceDefinitionTool = new GetInterfaceDefinitionTool(new TestLogger<GetInterfaceDefinitionTool>(TestOutput), _packageService, new ApiDefinitionFormatter(), archiveService);
        var enumDefinitionTool = new GetEnumDefinitionTool(new TestLogger<GetEnumDefinitionTool>(TestOutput), _packageService, new ApiDefinitionFormatter(), archiveService);

        var failures = new List<string>();
        var parsedOk = 0;
        var metaPackages = 0;
        var totalPublicTypes = 0;
        var totalPublicMethods = 0;

        foreach (var packageId in PopularPackages)
        {
            try
            {
                var version = await _packageService.GetLatestVersion(packageId);
                var loaded = await archiveService.LoadPackageMetadataAsync(packageId, version, VoidProgressNotifier);
                var allTypes = loaded.Api.Assemblies.SelectMany(static assembly => assembly.Types).ToList();
                var classes = allTypes.Where(static type => type.Kind is ApiTypeKind.Class or ApiTypeKind.StaticClass or ApiTypeKind.AbstractClass or ApiTypeKind.SealedClass or ApiTypeKind.RecordClass).ToList();
                var interfaces = allTypes.Where(static type => type.Kind == ApiTypeKind.Interface).ToList();
                var structs = allTypes.Where(static type => type.Kind is ApiTypeKind.Struct or ApiTypeKind.RecordStruct).ToList();
                var enums = allTypes.Where(static type => type.Kind == ApiTypeKind.Enum).ToList();
                var delegates = allTypes.Where(static type => type.Kind == ApiTypeKind.Delegate).ToList();
                var methodCount = allTypes.Sum(static type => type.Methods.Count);
                var propertyCount = allTypes.Sum(static type => type.Properties.Count);
                var eventCount = allTypes.Sum(static type => type.Events.Count);

                if (loaded.PackageInfo.IsMetaPackage)
                {
                    metaPackages++;
                }
                else
                {
                    Assert.NotEmpty(loaded.Api.Assemblies);
                    Assert.NotEmpty(allTypes);
                }

                if (Expectations.TryGetValue(packageId, out var expectation))
                {
                    if (expectation.MinPublicTypes is { } minTypes)
                    {
                        Assert.True(allTypes.Count >= minTypes, $"{packageId}: expected at least {minTypes} public types, got {allTypes.Count}.");
                    }

                    foreach (var expectedType in expectation.ExpectedTypeNames)
                    {
                        Assert.Contains(allTypes, type => type.FullName == expectedType);
                    }
                }

                var typeResult = await listTypesTool.list_classes_records_structs(packageId, version, maxResults: 10);
                var interfaceResult = await listInterfacesTool.list_interfaces(packageId, version);
                Assert.True(
                    loaded.PackageInfo.IsMetaPackage || typeResult.Types.Count > 0 || interfaceResult.Interfaces.Count > 0 || enums.Count > 0,
                    $"{packageId}: expected at least one tool to return API results.");

                foreach (var type in typeResult.Types.Take(3))
                {
                    var definition = await classDefinitionTool.get_class_or_record_or_struct_definition(packageId, type.FullName, version);
                    Assert.Contains(type.Name.Split('<')[0], definition);
                }

                foreach (var iface in interfaceResult.Interfaces.Take(3))
                {
                    var definition = await interfaceDefinitionTool.get_interface_definition(packageId, iface.FullName, version);
                    Assert.Contains("interface", definition);
                }

                foreach (var enumType in enums.Take(2))
                {
                    var definition = await enumDefinitionTool.get_enum_definition(packageId, enumType.FullName, version);
                    Assert.Contains("enum", definition);
                }

                totalPublicTypes += allTypes.Count;
                totalPublicMethods += methodCount;
                parsedOk++;

                TestOutput.WriteLine($"""
Package: {packageId}
Version: {version}
Selected TFM: {string.Join(", ", loaded.Api.Assemblies.Select(static assembly => assembly.TargetFramework).Distinct())}
Assemblies:
{string.Join(Environment.NewLine, loaded.Api.Assemblies.Select(static assembly => $"  - {assembly.PackagePath}"))}
Public types: {allTypes.Count}
Classes: {classes.Count}
Interfaces: {interfaces.Count}
Structs: {structs.Count}
Enums: {enums.Count}
Delegates: {delegates.Count}
Methods: {methodCount}
Properties: {propertyCount}
Events: {eventCount}
Status: OK
""");
            }
            catch (Exception ex)
            {
                failures.Add($"{packageId}: {ex.GetType().Name}: {ex.Message}");
                TestOutput.WriteLine($"Package: {packageId}{Environment.NewLine}Status: FAILED{Environment.NewLine}{ex}");
            }
        }

        TestOutput.WriteLine($"""
Total packages: {PopularPackages.Length}
Parsed OK: {parsedOk}
Failed: {failures.Count}
Meta-packages: {metaPackages}
Total public types: {totalPublicTypes}
Total public methods: {totalPublicMethods}
""");

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    public sealed record PackageSmokeExpectation
    {
        public required string PackageId { get; init; }
        public int? MinPublicTypes { get; init; }
        public int? MinClasses { get; init; }
        public int? MinInterfaces { get; init; }
        public string[] ExpectedTypeNames { get; init; } = [];
    }
}
