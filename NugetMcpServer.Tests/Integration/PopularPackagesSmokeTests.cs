using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NuGet.Packaging;
using NuGetMcpServer.Services;
using NuGetMcpServer.Services.Formatters;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;
using Xunit;

namespace NuGetMcpServer.Tests.Integration;

public class PopularNuGetPackagesMetadataSmokeTests : TestBase
{
    private readonly NuGetPackageService _packageService;

    public PopularNuGetPackagesMetadataSmokeTests(ITestOutputHelper output) : base(output)
    {
        _packageService = CreateNuGetPackageService();
    }

    public static TheoryData<string> PopularPackages => new()
    {
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
        "Moq",
    };

    [Theory]
    [Trait("Category", "Exploratory")]
    [Trait("Category", "Manual")]
    [MemberData(nameof(PopularPackages))]
    public async Task LoadPopularPackages_NoErrors(string packageId)
    {
        if (Environment.GetEnvironmentVariable("NUGET_MCP_RUN_EXPLORATORY_TESTS") != "1")
        {
            TestOutput.WriteLine("Skipping exploratory smoke test because NUGET_MCP_RUN_EXPLORATORY_TESTS is not 1.");
            return;
        }

        var archiveService = CreateArchiveProcessingService();

        var listTypesLogger = new TestLogger<ListTypesTool>(TestOutput);
        var classDefLogger = new TestLogger<GetClassDefinitionTool>(TestOutput);
        var listInterfacesLogger = new TestLogger<ListInterfacesTool>(TestOutput);
        var interfaceDefLogger = new TestLogger<GetInterfaceDefinitionTool>(TestOutput);

        var listTypesTool = new ListTypesTool(listTypesLogger, _packageService, archiveService);
        var classDefTool = new GetClassDefinitionTool(classDefLogger, _packageService, new ApiDefinitionFormatter(), archiveService);
        var listInterfacesTool = new ListInterfacesTool(listInterfacesLogger, _packageService, archiveService);
        var interfaceDefTool = new GetInterfaceDefinitionTool(interfaceDefLogger, _packageService, new ApiDefinitionFormatter(), archiveService);

        var version = await _packageService.GetLatestVersion(packageId);
        await using var stream = await _packageService.DownloadPackageAsync(packageId, version);
        using var reader = new PackageArchiveReader(stream, leaveStreamOpen: true);

        var dllFiles = ArchiveProcessingService.GetUniqueAssemblyFiles(reader);

        int classCount = 0;
        int interfaceCount = 0;
        int enumCount = 0;

        foreach (var file in dllFiles)
        {
            using var dllStream = reader.GetStream(file);
            using var msDll = new MemoryStream();
            dllStream.CopyTo(msDll);

            var (c, i, e) = CountTypes(msDll.ToArray());
            classCount += c;
            interfaceCount += i;
            enumCount += e;
        }

        TestOutput.WriteLine($"{packageId} v{version}: Classes={classCount}, Interfaces={interfaceCount}, Enums={enumCount}");

        var typeResult = await listTypesTool.list_classes_records_structs(packageId, version);
        TestOutput.WriteLine("list_classes_records_structs =>");
        TestOutput.WriteLine(typeResult.Format());
        foreach (var cls in typeResult.Types)
        {
            var def = await classDefTool.get_class_or_record_or_struct_definition(packageId, cls.FullName, version);
            Assert.False(string.IsNullOrWhiteSpace(def));
            TestOutput.WriteLine($"get_class_or_record_or_struct_definition({cls.FullName}) =>");
            TestOutput.WriteLine(def);
        }

        var interfaceResult = await listInterfacesTool.list_interfaces(packageId, version);
        TestOutput.WriteLine("list_interfaces =>");
        TestOutput.WriteLine(interfaceResult.Format());
        foreach (var iface in interfaceResult.Interfaces)
        {
            var def = await interfaceDefTool.get_interface_definition(packageId, iface.FullName, version);
            Assert.False(string.IsNullOrWhiteSpace(def));
            TestOutput.WriteLine($"get_interface_definition({iface.FullName}) =>");
            TestOutput.WriteLine(def);
        }

        var structTypes = typeResult.Types.Where(t => t.Kind == TypeKind.Struct || t.Kind == TypeKind.RecordStruct).ToList();
        var recordTypes = typeResult.Types.Where(t => t.Kind == TypeKind.RecordClass || t.Kind == TypeKind.RecordStruct).ToList();

        foreach (var st in structTypes)
        {
            var def = await classDefTool.get_class_or_record_or_struct_definition(packageId, st.FullName, version);
            Assert.False(string.IsNullOrWhiteSpace(def));
            TestOutput.WriteLine($"get_class_or_record_or_struct_definition({st.FullName}) =>");
            TestOutput.WriteLine(def);
        }

        foreach (var rec in recordTypes)
        {
            var def = await classDefTool.get_class_or_record_or_struct_definition(packageId, rec.FullName, version);
            Assert.False(string.IsNullOrWhiteSpace(def));
            TestOutput.WriteLine($"get_class_or_record_or_struct_definition({rec.FullName}) =>");
            TestOutput.WriteLine(def);
        }

        // Verify class flags against record and struct listings
        var structNames = structTypes.Select(s => s.FullName).ToHashSet();
        var recordLookup = recordTypes.ToDictionary(r => r.FullName, r => r.Kind == TypeKind.RecordStruct);

        foreach (var cls in typeResult.Types)
        {
            if (cls.Kind == TypeKind.Struct || cls.Kind == TypeKind.RecordStruct)
                Assert.Contains(cls.FullName, structNames);

            if (cls.Kind == TypeKind.RecordClass || cls.Kind == TypeKind.RecordStruct)
            {
                Assert.True(recordLookup.TryGetValue(cls.FullName, out bool recIsStruct));
                Assert.Equal(recIsStruct, cls.Kind == TypeKind.RecordStruct);
            }
        }

        Assert.DoesNotContain(listTypesLogger.Entries, e => e.Exception != null);
        Assert.DoesNotContain(classDefLogger.Entries, e => e.Exception != null);
        Assert.DoesNotContain(listInterfacesLogger.Entries, e => e.Exception != null);
        Assert.DoesNotContain(interfaceDefLogger.Entries, e => e.Exception != null);
    }

    private static (int classes, int interfaces, int enums) CountTypes(byte[] assemblyData)
    {
        using var ms = new MemoryStream(assemblyData);
        using var peReader = new PEReader(ms);
        var reader = peReader.GetMetadataReader();

        int classCount = 0;
        int interfaceCount = 0;
        int enumCount = 0;

        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(handle);

            var attrs = typeDef.Attributes;



            bool isInterface = (attrs & System.Reflection.TypeAttributes.Interface) != 0;

            bool isEnum = false;
            if (!isInterface && !typeDef.BaseType.IsNil)
            {
                var baseTypeName = string.Empty;
                var baseTypeNamespace = string.Empty;
                switch (typeDef.BaseType.Kind)
                {
                    case HandleKind.TypeReference:
                        var tr = reader.GetTypeReference((TypeReferenceHandle)typeDef.BaseType);
                        baseTypeName = reader.GetString(tr.Name);
                        baseTypeNamespace = reader.GetString(tr.Namespace);
                        break;
                    case HandleKind.TypeDefinition:
                        var td = reader.GetTypeDefinition((TypeDefinitionHandle)typeDef.BaseType);
                        baseTypeName = reader.GetString(td.Name);
                        baseTypeNamespace = reader.GetString(td.Namespace);
                        break;
                }

                if (baseTypeName == "Enum" && baseTypeNamespace == "System")
                    isEnum = true;
            }

            if (isInterface)
                interfaceCount++;
            else if (isEnum)
                enumCount++;
            else
                classCount++;
        }

        return (classCount, interfaceCount, enumCount);
    }
}
