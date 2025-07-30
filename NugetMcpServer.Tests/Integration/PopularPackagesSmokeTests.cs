using NuGet.Packaging;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Xunit;
using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Integration;

public class PopularPackagesSmokeTests : TestBase
{
    public PopularPackagesSmokeTests(ITestOutputHelper output) : base(output)
    {
    }

    public static TheoryData<string> PopularPackages => new()
    {
        "Newtonsoft.Json",
        "Serilog",
        "Autofac",
        "AutoMapper",
        "Dapper",
        "FluentValidation",
        "Polly",
        "NUnit",
        "MediatR",
        "xunit",
        "CsvHelper",
        "RestSharp",
        "NLog",
        "Swashbuckle.AspNetCore",
        "Moq",
        "MassTransit",
        "Hangfire.Core",
        "Quartz",
        "HtmlAgilityPack",
        "Humanizer",
        "Bogus",
        "IdentityModel",
        "Azure.Core",
        "MongoDB.Driver",
        "Grpc.Net.Client",
        "Microsoft.Extensions.Logging",
        "Microsoft.EntityFrameworkCore",
        "System.Text.Json",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Http"
    };

    [Theory]
    [MemberData(nameof(PopularPackages))]
    public async Task LoadPopularPackages_NoErrors(string packageId)
    {
        var packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
        var packageService = new NuGetPackageService(packageLogger, HttpClient, CreateMetaPackageDetector());

        var version = await packageService.GetLatestVersion(packageId);
        await using var stream = await packageService.DownloadPackageAsync(packageId, version);
        using var reader = new PackageArchiveReader(stream, leaveStreamOpen: true);

        var dllFiles = ArchiveProcessingService.GetUniqueAssemblyFiles(reader);

        var classNames = new List<string>();
        var interfaceNames = new List<string>();
        var enumNames = new List<string>();

        foreach (var file in dllFiles)
        {
            using var dllStream = reader.GetStream(file);
            using var msDll = new MemoryStream();
            dllStream.CopyTo(msDll);

            var (classes, interfaces, enums) = GetTypeNames(msDll.ToArray());
            classNames.AddRange(classes);
            interfaceNames.AddRange(interfaces);
            enumNames.AddRange(enums);
        }

        TestOutput.WriteLine($"{packageId} v{version}");
        TestOutput.WriteLine($"Classes ({classNames.Count}): {string.Join(", ", classNames)}");
        TestOutput.WriteLine($"Interfaces ({interfaceNames.Count}): {string.Join(", ", interfaceNames)}");
        TestOutput.WriteLine($"Enums ({enumNames.Count}): {string.Join(", ", enumNames)}");

        Assert.DoesNotContain(packageLogger.Entries, e => e.Exception != null);
    }

    private static (List<string> classes, List<string> interfaces, List<string> enums) GetTypeNames(byte[] assemblyData)
    {
        using var ms = new MemoryStream(assemblyData);
        using var peReader = new PEReader(ms);
        var reader = peReader.GetMetadataReader();

        var classes = new List<string>();
        var interfaces = new List<string>();
        var enums = new List<string>();

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

            var name = reader.GetString(typeDef.Name);
            var ns = reader.GetString(typeDef.Namespace);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            if (isInterface)
                interfaces.Add(fullName);
            else if (isEnum)
                enums.Add(fullName);
            else
                classes.Add(fullName);
        }

        return (classes, interfaces, enums);
    }
}
