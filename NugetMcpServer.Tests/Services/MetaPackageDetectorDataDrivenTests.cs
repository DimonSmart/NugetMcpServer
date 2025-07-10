using NuGetMcpServer.Extensions;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tests.Helpers;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Services;

public class MetaPackageDetectorDataDrivenTests : TestBase
{
    private readonly TestLogger<MetaPackageDetector> _logger;
    private readonly MetaPackageDetector _detector;
    private readonly NuGetPackageService _packageService;
    private readonly ListClassesTool _listClassesTool;
    private readonly ListInterfacesTool _listInterfacesTool;

    public MetaPackageDetectorDataDrivenTests(ITestOutputHelper testOutput) : base(testOutput)
    {
        _logger = new TestLogger<MetaPackageDetector>(TestOutput);
        _detector = new MetaPackageDetector(_logger);
        _packageService = CreateNuGetPackageService();

        var archiveProcessingService = CreateArchiveProcessingService();
        var classesLogger = new TestLogger<ListClassesTool>(TestOutput);
        _listClassesTool = new ListClassesTool(classesLogger, _packageService, archiveProcessingService);

        var interfacesLogger = new TestLogger<ListInterfacesTool>(TestOutput);
        _listInterfacesTool = new ListInterfacesTool(interfacesLogger, _packageService, archiveProcessingService);
    }

    public static TheoryData<string, bool> MetaPackageTestData => new()
    {
        // Known meta-packages that actually work
        { "NETStandard.Library", true },
        { "Microsoft.AspNetCore.All", true },
        { "Microsoft.NETCore.App", true },
        
        // xunit is actually a meta-package (consists of xunit.core + xunit.assert)
        { "xunit", true },
        
        // Known regular packages with actual assemblies
        { "Newtonsoft.Json", false },
        { "System.Text.Json", false },
        { "Microsoft.EntityFrameworkCore", false },
        { "Serilog", false },
        { "AutoMapper", false },
        { "FluentValidation", false }
    };

    [Theory]
    [MemberData(nameof(MetaPackageTestData))]
    public async Task IsMetaPackage_KnownPackages_ReturnsExpectedResult(string packageId, bool expectedIsMetaPackage)
    {
        // Arrange
        var version = await _packageService.GetLatestVersion(packageId);
        if (version == null)
        {
            throw new InvalidOperationException($"Could not get latest version for package {packageId}");
        }

        // Act
        using var packageStream = await _packageService.DownloadPackageAsync(packageId, version, ProgressNotifier.VoidProgressNotifier);
        var isMetaPackage = _detector.IsMetaPackage(packageStream, packageId);

        // Assert
        TestOutput.WriteLine($"Package: {packageId} v{version}");
        TestOutput.WriteLine($"Expected: {expectedIsMetaPackage}, Actual: {isMetaPackage}");

        Assert.Equal(expectedIsMetaPackage, isMetaPackage);
    }

    public static TheoryData<string, string[]> PackageClassesTestData => new()
    {
        // Package ID, Expected classes (subset)
        {
            "Newtonsoft.Json",
            new[] { "JsonConvert", "JsonSerializer", "JsonTextReader", "JsonTextWriter" }
        },
        {
            "System.Text.Json",
            new[] { "JsonSerializer", "JsonDocument", "JsonNode", "JsonException" }
        },
        {
            "Serilog",
            new[] { "Log", "Logger", "LoggerConfiguration" }
        },
        {
            "AutoMapper",
            new[] { "Mapper", "MapperConfiguration", "Profile" }
        }
    };

    [Theory]
    [MemberData(nameof(PackageClassesTestData))]
    public async Task GetClasses_KnownPackages_ContainsExpectedClasses(string packageId, string[] expectedClasses)
    {
        // Arrange
        var version = await _packageService.GetLatestVersion(packageId);
        if (version == null)
        {
            throw new InvalidOperationException($"Could not get latest version for package {packageId}");
        }

        // Act
        var result = await _listClassesTool.list_classes(packageId, version);

        // Assert
        TestOutput.WriteLine($"Package: {packageId} v{version}");
        TestOutput.WriteLine($"Total classes found: {result.Classes.Count}");
        TestOutput.WriteLine($"Expected classes: {string.Join(", ", expectedClasses)}");

        var classNames = result.Classes.Select(c => c.Name).ToHashSet();
        TestOutput.WriteLine($"Sample classes found: {string.Join(", ", classNames.Take(10))}");

        foreach (var expectedClass in expectedClasses)
        {
            Assert.Contains(expectedClass, classNames);
            TestOutput.WriteLine($"✓ Found expected class: {expectedClass}");
        }
    }

    public static TheoryData<string, string[]> PackageInterfacesTestData => new()
    {
        // Package ID, Expected interfaces (subset)
        {
            "Microsoft.Extensions.Logging.Abstractions",
            new[] { "ILogger", "ILoggerFactory", "ILoggerProvider" }
        },
        {
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            new[] { "IServiceCollection", "IServiceScope", "IServiceScopeFactory" }
        },
        {
            "System.Text.Json",
            new[] { "IJsonTypeInfoResolver" }
        }
    };

    [Theory]
    [MemberData(nameof(PackageInterfacesTestData))]
    public async Task GetInterfaces_KnownPackages_ContainsExpectedInterfaces(string packageId, string[] expectedInterfaces)
    {
        // Arrange
        var version = await _packageService.GetLatestVersion(packageId);
        if (version == null)
        {
            throw new InvalidOperationException($"Could not get latest version for package {packageId}");
        }

        // Act
        var result = await _listInterfacesTool.list_interfaces(packageId, version);

        // Assert
        TestOutput.WriteLine($"Package: {packageId} v{version}");
        TestOutput.WriteLine($"Total interfaces found: {result.Interfaces.Count}");
        TestOutput.WriteLine($"Expected interfaces: {string.Join(", ", expectedInterfaces)}");

        var interfaceNames = result.Interfaces.Select(i => i.Name).ToHashSet();
        TestOutput.WriteLine($"Sample interfaces found: {string.Join(", ", interfaceNames.Take(10))}");

        foreach (var expectedInterface in expectedInterfaces)
        {
            Assert.Contains(expectedInterface, interfaceNames);
            TestOutput.WriteLine($"✓ Found expected interface: {expectedInterface}");
        }
    }

    public static TheoryData<string, string[]> PackageDependenciesTestData => new()
    {
        {
            "Microsoft.EntityFrameworkCore",
            new[] { "Microsoft.EntityFrameworkCore.Abstractions", "Microsoft.Extensions.Caching.Memory" }
        },
        {
            "Microsoft.AspNetCore.Mvc",
            new[] { "Microsoft.AspNetCore.Mvc.ViewFeatures", "Microsoft.AspNetCore.Mvc.DataAnnotations" }
        },
        {
            "Microsoft.Extensions.Hosting",
            new[] { "Microsoft.Extensions.Hosting.Abstractions" }
        }
    };

    [Theory]
    [MemberData(nameof(PackageDependenciesTestData))]
    public async Task GetDependencies_KnownPackages_ContainsExpectedDependencies(string packageId, string[] expectedDependencies)
    {
        // Arrange
        var version = await _packageService.GetLatestVersion(packageId);
        if (version == null)
        {
            throw new InvalidOperationException($"Could not get latest version for package {packageId}");
        }

        // Act
        using var packageStream = await _packageService.DownloadPackageAsync(packageId, version, ProgressNotifier.VoidProgressNotifier);
        var packageInfo = _packageService.GetPackageInfoAsync(packageStream, packageId, version);

        // Assert
        TestOutput.WriteLine($"Package: {packageId} v{version}");
        TestOutput.WriteLine($"Total dependencies found: {packageInfo.Dependencies.Count}");
        TestOutput.WriteLine($"Expected dependencies: {string.Join(", ", expectedDependencies)}");

        var dependencyIds = packageInfo.Dependencies.Select(d => d.Id).ToHashSet();
        TestOutput.WriteLine($"Sample dependencies found: {string.Join(", ", dependencyIds.Take(10))}");

        foreach (var expectedDependency in expectedDependencies)
        {
            Assert.Contains(expectedDependency, dependencyIds);
            TestOutput.WriteLine($"✓ Found expected dependency: {expectedDependency}");
        }
    }

    [Theory]
    [InlineData("NonExistentPackage12345XYZ")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetLatestVersion_InvalidPackageId_ThrowsException(string invalidPackageId)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => _packageService.GetLatestVersion(invalidPackageId));

        TestOutput.WriteLine($"Package ID: '{invalidPackageId}' -> Exception: {exception.Message}");
        Assert.Contains("404", exception.Message);
    }

    [Fact]
    public async Task MetaPackageDetection_ConsistentResults_ForSamePackage()
    {
        // Test that meta-package detection is consistent across multiple calls
        const string packageId = "xunit"; // Using a package that is detected as meta

        var version = await _packageService.GetLatestVersion(packageId);
        if (version == null)
        {
            throw new InvalidOperationException($"Could not get latest version for package {packageId}");
        }

        // Run the test multiple times to ensure consistency
        var results = new List<bool>();

        for (int i = 0; i < 3; i++)
        {
            using var packageStream = await _packageService.DownloadPackageAsync(packageId, version, ProgressNotifier.VoidProgressNotifier);
            var isMetaPackage = _detector.IsMetaPackage(packageStream, packageId);
            results.Add(isMetaPackage);
            TestOutput.WriteLine($"Iteration {i + 1}: {isMetaPackage}");
        }

        // All results should be the same
        Assert.True(results.All(r => r == results[0]), "Meta-package detection should return consistent results");
        Assert.True(results[0], $"{packageId} should be detected as a meta-package");
    }

    [Fact]
    public async Task PackageExtraction_HandlesCorruptedData_Gracefully()
    {
        // Test with a valid package first to ensure the system works
        const string validPackageId = "Newtonsoft.Json";
        var version = await _packageService.GetLatestVersion(validPackageId);

        if (version == null)
        {
            throw new InvalidOperationException($"Could not get latest version for package {validPackageId}");
        }

        // Download and then corrupt the stream
        using var originalStream = await _packageService.DownloadPackageAsync(validPackageId, version, ProgressNotifier.VoidProgressNotifier);

        // Create a corrupted stream (truncate it)
        var originalData = originalStream.ToArray();
        var corruptedData = originalData.Take(originalData.Length / 2).ToArray();
        using var corruptedStream = new MemoryStream(corruptedData);

        // The detector should handle corruption gracefully
        var isMetaPackage = _detector.IsMetaPackage(corruptedStream, validPackageId);

        TestOutput.WriteLine($"Corrupted package detection result: {isMetaPackage}");

        // Should not throw an exception, even if the result might be incorrect
        Assert.True(true, "Detector should handle corrupted data gracefully without throwing exceptions");
    }
}
