using NugetMcpServer.Tests.Helpers;
using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;
using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Tools
{
    public class GetInterfaceDefinitionToolTests : TestBase
    {
        private readonly TestLogger<NuGetPackageService> _packageLogger;
        private readonly TestLogger<GetInterfaceDefinitionTool> _defToolLogger;
        private readonly NuGetPackageService _packageService;
        private readonly InterfaceFormattingService _formattingService;
        private readonly GetInterfaceDefinitionTool _defTool;

        public GetInterfaceDefinitionToolTests(ITestOutputHelper testOutput) : base(testOutput)
        {
            _packageLogger = new TestLogger<NuGetPackageService>(TestOutput);
            _defToolLogger = new TestLogger<GetInterfaceDefinitionTool>(TestOutput);

            _packageService = new NuGetPackageService(_packageLogger, HttpClient);
            _formattingService = new InterfaceFormattingService();
            _defTool = new GetInterfaceDefinitionTool(_defToolLogger, _packageService, _formattingService);
        }

        [Fact]
        public async Task GetInterfaceDefinition_WithSpecificInterface_ReturnsDefinition()
        {
            // Test with a known package and interface
            var packageId = "DimonSmart.MazeGenerator";
            var interfaceName = "ICell";
            var version = await _packageService.GetLatestVersion(packageId);

            // Get interface definition
            var definition = await _defTool.GetInterfaceDefinition(packageId, interfaceName, version);

            // Assert
            Assert.NotNull(definition);
            Assert.Contains("interface", definition);
            Assert.Contains("ICell", definition);

            TestOutput.WriteLine("\n========== TEST OUTPUT: ICell INTERFACE DEFINITION ==========");
            TestOutput.WriteLine(definition);
            TestOutput.WriteLine("================================================================\n");
        }

        [Fact]
        public async Task GetInterfaceDefinition_WithGenericInterface_ReturnsFormattedDefinition()
        {
            // Test with a known generic interface
            var packageId = "DimonSmart.MazeGenerator";
            var interfaceName = "IMaze";  // Generic interface (actually IMaze<T>)
            var version = await _packageService.GetLatestVersion(packageId);

            // Get interface definition
            var definition = await _defTool.GetInterfaceDefinition(packageId, interfaceName, version);

            // Assert
            Assert.NotNull(definition);
            Assert.Contains("interface", definition);
            Assert.Contains("IMaze<", definition);  // Should be formatted as IMaze<T>, not IMaze`1
            Assert.DoesNotContain("not found in package", definition);

            TestOutput.WriteLine("\n========== TEST OUTPUT: IMaze INTERFACE DEFINITION ==========");
            TestOutput.WriteLine(definition);
            TestOutput.WriteLine("================================================================\n");
        }

        [Fact]
        public async Task GetInterfaceDefinition_WithPackageAndListTool_WorksWithBothTools()
        {
            // This test verifies that both tools can work together on the same package
            var listToolLogger = new TestLogger<ListInterfacesTool>(TestOutput);
            var listTool = new ListInterfacesTool(listToolLogger, _packageService);

            var packageId = "DimonSmart.MazeGenerator";

            // Step 1: List interfaces in the package
            var result = await listTool.ListInterfaces(packageId);
            Assert.NotNull(result);
            Assert.NotEmpty(result.Interfaces);

            // Step 2: Get definition of one of the interfaces
            var mazeInterface = result.Interfaces.FirstOrDefault(i => i.Name.StartsWith("IMaze"));
            if (mazeInterface != null)
            {
                var definition = await _defTool.GetInterfaceDefinition(
                    packageId,
                    mazeInterface.Name,
                    result.Version);

                // Assert
                Assert.Contains("interface", definition);
                Assert.Contains("IMaze<", definition);

                TestOutput.WriteLine("\n========== TEST OUTPUT: RESULT OF GetInterfaceDefinition ==========");
                TestOutput.WriteLine(definition);
                TestOutput.WriteLine("================================================================\n");
            }
        }

        [Fact]
        public async Task GetInterfaceDefinition_WithFullInterfaceName_ReturnsDefinition()
        {
            // Test with full interface name including namespace
            var packageId = "DimonSmart.MazeGenerator";
            var version = await _packageService.GetLatestVersion(packageId);

            // First, get the list of interfaces to find a full name
            var listToolLogger = new TestLogger<ListInterfacesTool>(TestOutput);
            var listTool = new ListInterfacesTool(listToolLogger, _packageService);
            var result = await listTool.ListInterfaces(packageId);

            // Find an interface with a namespace
            var interfaceWithNamespace = result.Interfaces
                .FirstOrDefault(i => !string.IsNullOrEmpty(i.FullName) && i.FullName.Contains('.'));

            if (interfaceWithNamespace != null)
            {
                // Test with full name (including namespace)
                var definition = await _defTool.GetInterfaceDefinition(
                    packageId,
                    interfaceWithNamespace.FullName,
                    version);

                // Assert
                Assert.NotNull(definition);
                Assert.Contains("interface", definition);
                Assert.DoesNotContain("not found in package", definition);

                TestOutput.WriteLine($"\n========== TEST OUTPUT: FULL NAME INTERFACE ({interfaceWithNamespace.FullName}) ==========");
                TestOutput.WriteLine(definition);
                TestOutput.WriteLine("================================================================\n");
            }
        }

        [Fact]
        public async Task GetInterfaceDefinition_WithFullGenericInterfaceName_ReturnsDefinition()
        {
            // Test with full generic interface name including namespace (e.g. "Namespace.IInterface`1")
            var packageId = "DimonSmart.MazeGenerator";
            var version = await _packageService.GetLatestVersion(packageId);

            // First, get the list of interfaces to find a generic interface
            var listToolLogger = new TestLogger<ListInterfacesTool>(TestOutput);
            var listTool = new ListInterfacesTool(listToolLogger, _packageService);
            var result = await listTool.ListInterfaces(packageId);

            // Find a generic interface (contains backtick)
            var genericInterface = result.Interfaces
                .FirstOrDefault(i => !string.IsNullOrEmpty(i.FullName) && i.FullName.Contains('`'));

            if (genericInterface != null)
            {
                // Test with full name including backtick notation (e.g. "Namespace.IMaze`1")
                var definition = await _defTool.GetInterfaceDefinition(
                    packageId,
                    genericInterface.FullName,
                    version);

                // Assert
                Assert.NotNull(definition);
                Assert.Contains("interface", definition);
                Assert.DoesNotContain("not found in package", definition);

                TestOutput.WriteLine($"\n========== TEST OUTPUT: FULL GENERIC INTERFACE ({genericInterface.FullName}) ==========");
                TestOutput.WriteLine(definition);
                TestOutput.WriteLine("================================================================\n");

                // Also test with the base name (without `1)
                if (genericInterface.FullName.Contains('`'))
                {
                    var baseName = genericInterface.FullName.Substring(0, genericInterface.FullName.IndexOf('`'));
                    var definitionByBaseName = await _defTool.GetInterfaceDefinition(
                        packageId,
                        baseName,
                        version);

                    Assert.NotNull(definitionByBaseName);
                    Assert.Contains("interface", definitionByBaseName);
                    Assert.DoesNotContain("not found in package", definitionByBaseName);

                    TestOutput.WriteLine($"\n========== TEST OUTPUT: GENERIC INTERFACE BY BASE NAME ({baseName}) ==========");
                    TestOutput.WriteLine(definitionByBaseName);
                    TestOutput.WriteLine("================================================================\n");
                }
            }
        }

        [Fact]
        public async Task GetInterfaceDefinition_WithVariousNameFormats_HandlesAllCases()
        {
            // This test verifies that interface matching works with different name formats
            var packageId = "DimonSmart.MazeGenerator";
            var version = await _packageService.GetLatestVersion(packageId);

            // Get list of interfaces to test with
            var listToolLogger = new TestLogger<ListInterfacesTool>(TestOutput);
            var listTool = new ListInterfacesTool(listToolLogger, _packageService);
            var result = await listTool.ListInterfaces(packageId);

            Assert.NotEmpty(result.Interfaces);

            // Test different scenarios
            foreach (var iface in result.Interfaces.Take(2)) // Test first 2 interfaces
            {
                TestOutput.WriteLine($"\n=== Testing interface: {iface.Name} (Full: {iface.FullName}) ===");

                // Test 1: Short name
                var shortNameResult = await _defTool.GetInterfaceDefinition(packageId, iface.Name, version);
                Assert.NotNull(shortNameResult);
                Assert.Contains("interface", shortNameResult);
                Assert.DoesNotContain("not found in package", shortNameResult);
                TestOutput.WriteLine($"✓ Short name '{iface.Name}' works");

                // Test 2: Full name (if available and contains namespace)
                if (!string.IsNullOrEmpty(iface.FullName) && iface.FullName.Contains('.'))
                {
                    var fullNameResult = await _defTool.GetInterfaceDefinition(packageId, iface.FullName, version);
                    Assert.NotNull(fullNameResult);
                    Assert.Contains("interface", fullNameResult);
                    Assert.DoesNotContain("not found in package", fullNameResult);
                    TestOutput.WriteLine($"✓ Full name '{iface.FullName}' works");
                }

                // Test 3: Generic interface base name (if generic)
                if (iface.FullName?.Contains('`') == true)
                {
                    var backtickIndex = iface.FullName.IndexOf('`');
                    var baseName = iface.FullName.Substring(0, backtickIndex);
                    
                    var baseNameResult = await _defTool.GetInterfaceDefinition(packageId, baseName, version);
                    Assert.NotNull(baseNameResult);
                    Assert.Contains("interface", baseNameResult);
                    Assert.DoesNotContain("not found in package", baseNameResult);
                    TestOutput.WriteLine($"✓ Generic base name '{baseName}' works");

                    // Test 4: Short generic base name
                    var shortBacktickIndex = iface.Name.IndexOf('`');
                    if (shortBacktickIndex > 0)
                    {
                        var shortBaseName = iface.Name.Substring(0, shortBacktickIndex);
                        var shortBaseNameResult = await _defTool.GetInterfaceDefinition(packageId, shortBaseName, version);
                        Assert.NotNull(shortBaseNameResult);
                        Assert.Contains("interface", shortBaseNameResult);
                        Assert.DoesNotContain("not found in package", shortBaseNameResult);
                        TestOutput.WriteLine($"✓ Short generic base name '{shortBaseName}' works");
                    }
                }
            }
        }
    }
}
