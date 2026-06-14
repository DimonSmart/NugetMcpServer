using Microsoft.Extensions.Logging.Abstractions;
using NuGetMcpServer.Services;
using Xunit;

namespace NuGetMcpServer.Tests.Services;

public sealed class PackageMetadataReaderTests
{
    [Fact]
    public void ReadPackageApi_DecodesSyntheticEdgeCases()
    {
        var bytes = File.ReadAllBytes(FindTestLibrary("TestLibrary.V1"));
        var reader = new PackageMetadataReader(NullLogger<PackageMetadataReader>.Instance);

        var api = reader.ReadPackageApi(
            "TestLibrary.V1",
            "1.0.0",
            [
                new PackageAssemblyFile
                {
                    PackagePath = "lib/net10.0/TestLibrary.V1.dll",
                    FileName = "TestLibrary.V1.dll",
                    TargetFramework = "net10.0",
                    Bytes = bytes
                }
            ]);

        var assembly = Assert.Single(api.Assemblies);
        var edge = Assert.Single(assembly.Types.Where(static type => type.FullName.StartsWith("TestLibrary.MetadataEdgeCases", StringComparison.Ordinal)));

        Assert.Equal(ApiTypeKind.Class, edge.Kind);
        Assert.Contains(edge.GenericParameters, p =>
            p.Name == "T" &&
            p.HasReferenceTypeConstraint &&
            p.HasDefaultConstructorConstraint);
        Assert.Contains("TestLibrary.IMetadataGenericInterface<T>", edge.Interfaces);
        Assert.Contains(edge.Properties, property => property.Name == "NullableText" && property.Type == "string?");
        Assert.Contains(edge.Properties, property => property.Name == "Item" && property.IndexParameters.Single().Type == "int");
        Assert.Contains(edge.Events, evt => evt.Name == "Changed");
        Assert.Contains(edge.Fields, field => field.Name == "ConstValue" && field.IsConst && field.LiteralValue == "42");
        Assert.Contains(edge.Fields, field => field.Name == "ReadOnlyValue" && field.IsStatic && field.IsReadOnly);
        Assert.Contains(edge.Methods, method => method.Identity == "M:Overload`0(int)");
        Assert.Contains(edge.Methods, method => method.Identity == "M:Overload`0(string)");
        Assert.Contains(edge.Methods, method => method.Name == "ParamsMethod" && method.Parameters.Single().IsParams);
        Assert.Contains(edge.Methods, method => method.Name == "ObsoleteMethod" && method.IsObsolete);
    }

    [Fact]
    public void ApiDefinitionFormatter_FormatsEnumAndInterfaceDefinitions()
    {
        var bytes = File.ReadAllBytes(FindTestLibrary("TestLibrary.V1"));
        var reader = new PackageMetadataReader(NullLogger<PackageMetadataReader>.Instance);
        var api = reader.ReadPackageApi(
            "TestLibrary.V1",
            "1.0.0",
            [
                new PackageAssemblyFile
                {
                    PackagePath = "lib/net10.0/TestLibrary.V1.dll",
                    FileName = "TestLibrary.V1.dll",
                    TargetFramework = "net10.0",
                    Bytes = bytes
                }
            ]);
        var formatter = new ApiDefinitionFormatter();

        var iface = api.Assemblies.SelectMany(static a => a.Types).Single(static type => type.Name == "IMetadataGenericInterface<T>");
        var enumType = api.Assemblies.SelectMany(static a => a.Types).Single(static type => type.Name == "MetadataExplicitEnum");

        var ifaceText = formatter.FormatTypeDefinition(iface, api.PackageId, api.Version, "net10.0");
        var enumText = formatter.FormatTypeDefinition(enumType, api.PackageId, api.Version, "net10.0");

        Assert.Contains("interface IMetadataGenericInterface<T>", ifaceText);
        Assert.Contains("where T : class, new()", ifaceText);
        Assert.Contains("enum MetadataExplicitEnum", enumText);
        Assert.Contains("First = 10", enumText);
    }

    [Fact]
    public void ReadPackageApi_SkipsCorruptedDll()
    {
        var reader = new PackageMetadataReader(NullLogger<PackageMetadataReader>.Instance);

        var api = reader.ReadPackageApi(
            "Broken",
            "1.0.0",
            [
                new PackageAssemblyFile
                {
                    PackagePath = "lib/net10.0/Broken.dll",
                    FileName = "Broken.dll",
                    TargetFramework = "net10.0",
                    Bytes = [1, 2, 3, 4]
                }
            ]);

        Assert.Empty(api.Assemblies);
    }

    private static string FindTestLibrary(string projectName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "TestLibraries", projectName, "bin", "Debug", "net10.0", $"{projectName}.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find compiled {projectName}.dll");
    }
}
