using NuGetMcpServer.Models;
using NuGetMcpServer.Services;
using NuGetMcpServer.Services.Formatters;

namespace NuGetMcpServer.Tests.Services;

public class PackageResultBaseTests
{
    [Fact]
    public void ClassListResult_WithMetaPackage_ShowsMetaPackageHeader()
    {
        var result = new ClassListResult
        {
            PackageId = "Microsoft.SemanticKernel",
            Version = "1.60.0",
            IsMetaPackage = true,
            Description = "Semantic Kernel common package collection",
            Dependencies = new List<PackageDependency>
            {
                new PackageDependency { Id = "Microsoft.SemanticKernel.Core", Version = "1.60.0" },
                new PackageDependency { Id = "Microsoft.SemanticKernel.Connectors.AzureOpenAI", Version = "1.60.0" }
            }
        };

        var formatted = result.Format();

        Assert.Contains("META-PACKAGE: Microsoft.SemanticKernel v1.60.0", formatted);
        Assert.Contains("This package groups other related packages together", formatted);
        Assert.Contains("Microsoft.SemanticKernel.Core", formatted);
        Assert.Contains("Microsoft.SemanticKernel.Connectors.AzureOpenAI", formatted);
        Assert.Contains("💡 To see actual implementations, analyze one of the dependency packages", formatted);
    }

    [Fact]
    public void ClassListResult_WithMetaPackageAndOwnClasses_ShowsBoth()
    {
        var result = new ClassListResult
        {
            PackageId = "SomeMetaPackage",
            Version = "1.0.0",
            IsMetaPackage = true,
            Dependencies = new List<PackageDependency>
            {
                new PackageDependency { Id = "SomePackage.Core", Version = "1.0.0" }
            },
            Classes = new List<ClassInfo>
            {
                new ClassInfo { Name = "MetaClass", FullName = "SomeMetaPackage.MetaClass", AssemblyName = "SomeMetaPackage.dll" }
            }
        };

        var formatted = result.Format();

        Assert.Contains("META-PACKAGE: SomeMetaPackage v1.0.0", formatted);
        Assert.Contains("SomePackage.Core", formatted);
        Assert.Contains("MetaClass", formatted);
        Assert.DoesNotContain("To see actual classes and interfaces, please analyze one of the dependency packages", formatted);
    }

    [Fact]
    public void ClassListResult_WithRegularPackage_ShowsClassHeader()
    {
        var result = new ClassListResult
        {
            PackageId = "Newtonsoft.Json",
            Version = "13.0.3",
            IsMetaPackage = false,
            Classes = new List<ClassInfo>()
        };

        var formatted = result.Format();

        Assert.Contains("No public classes found in this package", formatted);
        Assert.DoesNotContain("META-PACKAGE", formatted);
    }

    [Fact]
    public void InterfaceListResult_WithMetaPackage_ShowsMetaPackageHeader()
    {
        var result = new InterfaceListResult
        {
            PackageId = "Microsoft.SemanticKernel",
            Version = "1.60.0",
            IsMetaPackage = true,
            Dependencies = new List<PackageDependency>
            {
                new PackageDependency { Id = "Microsoft.SemanticKernel.Core", Version = "1.60.0" }
            }
        };

        var formatted = result.Format();

        Assert.Contains("META-PACKAGE: Microsoft.SemanticKernel v1.60.0", formatted);
        Assert.Contains("Microsoft.SemanticKernel.Core", formatted);
    }

    [Fact]
    public void InterfaceListResult_WithMetaPackageAndOwnInterfaces_ShowsBoth()
    {
        var result = new InterfaceListResult
        {
            PackageId = "SomeMetaPackage",
            Version = "1.0.0",
            IsMetaPackage = true,
            Dependencies = new List<PackageDependency>
            {
                new PackageDependency { Id = "SomePackage.Core", Version = "1.0.0" }
            },
            Interfaces = new List<InterfaceInfo>
            {
                new InterfaceInfo { Name = "IMetaInterface", FullName = "SomeMetaPackage.IMetaInterface", AssemblyName = "SomeMetaPackage.dll" }
            }
        };

        var formatted = result.Format();

        Assert.Contains("META-PACKAGE: SomeMetaPackage v1.0.0", formatted);
        Assert.Contains("SomePackage.Core", formatted);
        Assert.Contains("This meta-package also contains the following interfaces", formatted);
        Assert.Contains("IMetaInterface", formatted);
        Assert.DoesNotContain("To see actual classes and interfaces, please analyze one of the dependency packages", formatted);
    }
}
