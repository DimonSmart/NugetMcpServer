using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NuGet.Packaging;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;
using Xunit;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Integration;

public class PopularPackagesSmokeTests : TestBase
{
    private readonly NuGetPackageService _packageService;

    public PopularPackagesSmokeTests(ITestOutputHelper output) : base(output)
    {
        _packageService = CreateNuGetPackageService();
    }

    [Fact]
    public async Task LoadPopularPackages_NoErrors()
    {
        var packages = new[]
        {
           // "MediatR",
           // "Newtonsoft.Json",
           // "Serilog",
           // "Autofac",
            "AutoMapper",
            "Dapper",
           // "FluentValidation",
           // "Polly",
           // "NUnit",
            "xunit",
            "CsvHelper",
           // "RestSharp",
            "NLog",
            "Swashbuckle.AspNetCore",
            "Moq",
            "Hangfire.Core",
            "Quartz",
            "HtmlAgilityPack",
            "Humanizer",
            "Bogus",
            "IdentityModel",
            // "Azure.Core",
            // "MongoDB.Driver",
            // "Grpc.Net.Client",
            "Microsoft.Extensions.Logging",
            "Microsoft.EntityFrameworkCore",
            "System.Text.Json",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Http"
        };

        var archiveService = CreateArchiveProcessingService();
        var listClassesTool = new ListClassesTool(new TestLogger<ListClassesTool>(TestOutput), _packageService, archiveService);
        var classDefTool = new GetClassDefinitionTool(new TestLogger<GetClassDefinitionTool>(TestOutput), _packageService, new ClassFormattingService(), archiveService);
        var listInterfacesTool = new ListInterfacesTool(new TestLogger<ListInterfacesTool>(TestOutput), _packageService, archiveService);
        var interfaceDefTool = new GetInterfaceDefinitionTool(new TestLogger<GetInterfaceDefinitionTool>(TestOutput), _packageService, new InterfaceFormattingService(), archiveService);
        var listStructsTool = new ListStructsTool(new TestLogger<ListStructsTool>(TestOutput), _packageService, archiveService);
        var structDefTool = new GetStructDefinitionTool(new TestLogger<GetStructDefinitionTool>(TestOutput), _packageService, new ClassFormattingService(), archiveService);
        var listRecordsTool = new ListRecordsTool(new TestLogger<ListRecordsTool>(TestOutput), _packageService, archiveService);
        var recordDefTool = new GetRecordDefinitionTool(new TestLogger<GetRecordDefinitionTool>(TestOutput), _packageService, new ClassFormattingService(), archiveService);

        foreach (var packageId in packages)
        {
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

            var classResult = await listClassesTool.list_classes_and_records(packageId, version);
            foreach (var cls in classResult.Classes)
            {
                var def = await classDefTool.get_class_or_record_definition(packageId, cls.FullName, version);
                Assert.False(string.IsNullOrWhiteSpace(def));
            }

            var interfaceResult = await listInterfacesTool.list_interfaces(packageId, version);
            foreach (var iface in interfaceResult.Interfaces)
            {
                var def = await interfaceDefTool.get_interface_definition(packageId, iface.FullName, version);
                Assert.False(string.IsNullOrWhiteSpace(def));
            }

            var structResult = await listStructsTool.list_structs(packageId, version);
            foreach (var st in structResult.Structs)
            {
                var def = await structDefTool.get_struct_definition(packageId, st.FullName, version);
                Assert.False(string.IsNullOrWhiteSpace(def));
            }

            var recordResult = await listRecordsTool.list_records(packageId, version);
            foreach (var rec in recordResult.Records)
            {
                var def = await recordDefTool.get_record_definition(packageId, rec.FullName, version);
                Assert.False(string.IsNullOrWhiteSpace(def));
            }
        }
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

            bool isNested = (attrs & System.Reflection.TypeAttributes.NestedFamANDAssem) != 0 ||
                            (attrs & System.Reflection.TypeAttributes.NestedAssembly) != 0 ||
                            (attrs & System.Reflection.TypeAttributes.NestedPrivate) != 0 ||
                            (attrs & System.Reflection.TypeAttributes.NestedFamily) != 0 ||
                            (attrs & System.Reflection.TypeAttributes.NestedFamORAssem) != 0 ||
                            (attrs & System.Reflection.TypeAttributes.NestedPublic) != 0;

            if (isNested)
                continue;

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
