using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NuGet.Packaging;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;
using System.Linq;
using NuGetMcpServer.Services.Formatters;
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

    public static TheoryData<string> PopularPackages => new()
    {
        // DimonSmart packages
        "DimonSmart.MazeGenerator",
        "DimonSmart.FileByContentComparer",
        "DimonSmart.TinyBenchmark",
        "DimonSmart.StringTrimmer",
        "DimonSmart.StringDiff",
        "DimonSmart.BuilderGenerator",
        "DimonSmart.StringTrimmerGenerator",
        "DimonSmart.Specification",
        "DimonSmart.RegexUnitTester.TestAdapter",
        "DimonSmart.RegexUnitTester.Attributes",
        "DimonSmart.Utils.Progress",
        "DimonSmart.AiUtils",
        "DimonSmart.IndentedStringBuilder",
        "DimonSmart.CustomizedDictionary",
        "DimonSmart.HashX",
        "DimonSmart.StronglyTypedDictionary",

        // Top 20 popular NuGet packages by downloads
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Logging",
        "Microsoft.Bcl.AsyncInterfaces",
        "Microsoft.Win32.SystemEvents",
        "Serilog",
        "Microsoft.Identity.Client",
        "System.Windows.Extensions",
        "Microsoft.Extensions.Http",
        "System.Security.Cryptography.Pkcs",
        "System.Diagnostics.EventLog",
        "Azure.Identity",
        "System.Threading.Channels",

        // SLOW!!! HUGE
        //  "AWSSDK.Core",
        // "Microsoft.EntityFrameworkCore",
        // "Microsoft.IdentityModel.Abstractions",
        // "Microsoft.Identity.Client",
        // "System.Drawing.Common",
        // "Newtonsoft.Json",
        // "Castle.Core",
        // "System.Text.Json",
        // "Azure.Core",
    };

    [Theory]
    [MemberData(nameof(PopularPackages))]
    public async Task LoadPopularPackages_NoErrors(string packageId)
    {
        var archiveService = CreateArchiveProcessingService();

        var listClassesLogger = new TestLogger<ListClassesTool>(TestOutput);
        var classDefLogger = new TestLogger<GetClassDefinitionTool>(TestOutput);
        var listInterfacesLogger = new TestLogger<ListInterfacesTool>(TestOutput);
        var interfaceDefLogger = new TestLogger<GetInterfaceDefinitionTool>(TestOutput);
        var listStructsLogger = new TestLogger<ListStructsTool>(TestOutput);
        var structDefLogger = new TestLogger<GetStructDefinitionTool>(TestOutput);
        var listRecordsLogger = new TestLogger<ListRecordsTool>(TestOutput);
        var recordDefLogger = new TestLogger<GetRecordDefinitionTool>(TestOutput);

        var listClassesTool = new ListClassesTool(listClassesLogger, _packageService, archiveService);
        var classDefTool = new GetClassDefinitionTool(classDefLogger, _packageService, new ClassFormattingService(), archiveService);
        var listInterfacesTool = new ListInterfacesTool(listInterfacesLogger, _packageService, archiveService);
        var interfaceDefTool = new GetInterfaceDefinitionTool(interfaceDefLogger, _packageService, new InterfaceFormattingService(), archiveService);
        var listStructsTool = new ListStructsTool(listStructsLogger, _packageService, archiveService);
        var structDefTool = new GetStructDefinitionTool(structDefLogger, _packageService, new ClassFormattingService(), archiveService);
        var listRecordsTool = new ListRecordsTool(listRecordsLogger, _packageService, archiveService);
        var recordDefTool = new GetRecordDefinitionTool(recordDefLogger, _packageService, new ClassFormattingService(), archiveService);

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
        TestOutput.WriteLine("list_classes_and_records =>");
        TestOutput.WriteLine(classResult.Format());
        foreach (var cls in classResult.Classes)
        {
            var def = await classDefTool.get_class_or_record_definition(packageId, cls.FullName, version);
            Assert.False(string.IsNullOrWhiteSpace(def));
            TestOutput.WriteLine($"get_class_or_record_definition({cls.FullName}) =>");
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

        var structResult = await listStructsTool.list_structs(packageId, version);
        TestOutput.WriteLine("list_structs =>");
        TestOutput.WriteLine(structResult.Format());
        foreach (var st in structResult.Structs)
        {
            var def = await structDefTool.get_struct_definition(packageId, st.FullName, version);
            Assert.False(string.IsNullOrWhiteSpace(def));
            TestOutput.WriteLine($"get_struct_definition({st.FullName}) =>");
            TestOutput.WriteLine(def);
        }

        var recordResult = await listRecordsTool.list_records(packageId, version);
        TestOutput.WriteLine("list_records =>");
        TestOutput.WriteLine(recordResult.Format());
        foreach (var rec in recordResult.Records)
        {
            var def = await recordDefTool.get_record_definition(packageId, rec.FullName, version);
            Assert.False(string.IsNullOrWhiteSpace(def));
            TestOutput.WriteLine($"get_record_definition({rec.FullName}) =>");
            TestOutput.WriteLine(def);
        }

        Assert.Empty(listClassesLogger.Entries.Where(e => e.Exception != null));
        Assert.Empty(classDefLogger.Entries.Where(e => e.Exception != null));
        Assert.Empty(listInterfacesLogger.Entries.Where(e => e.Exception != null));
        Assert.Empty(interfaceDefLogger.Entries.Where(e => e.Exception != null));
        Assert.Empty(listStructsLogger.Entries.Where(e => e.Exception != null));
        Assert.Empty(structDefLogger.Entries.Where(e => e.Exception != null));
        Assert.Empty(listRecordsLogger.Entries.Where(e => e.Exception != null));
        Assert.Empty(recordDefLogger.Entries.Where(e => e.Exception != null));
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
