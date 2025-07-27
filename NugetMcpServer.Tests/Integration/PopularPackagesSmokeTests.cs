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
