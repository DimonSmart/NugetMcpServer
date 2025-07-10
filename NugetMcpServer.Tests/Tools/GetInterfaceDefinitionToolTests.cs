using NuGetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;
using NuGetMcpServer.Tools;

using Xunit.Abstractions;

namespace NuGetMcpServer.Tests.Tools
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

            _packageService = CreateNuGetPackageService();
            _formattingService = new InterfaceFormattingService();
            var archiveService = new ArchiveProcessingService(new TestLogger<ArchiveProcessingService>(TestOutput), _packageService);
            _defTool = new GetInterfaceDefinitionTool(_defToolLogger, _packageService, _formattingService, archiveService);
        }

        [Fact]
        public async Task GetInterfaceDefinition_WithSpecificInterface_ReturnsDefinition()
        {
            // Test with a known package and interface
            var packageId = "DimonSmart.MazeGenerator";
            var interfaceName = "ICell";
            var version = await _packageService.GetLatestVersion(packageId);

            var definition = await _defTool.get_interface_definition(packageId, interfaceName, version);

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
            var packageId = "DimonSmart.MazeGenerator";
            var genericMazeInterfaceName = "IMaze";
            var version = await _packageService.GetLatestVersion(packageId);

            var definition = await _defTool.get_interface_definition(packageId, genericMazeInterfaceName, version);

            Assert.NotNull(definition);
            Assert.Contains("interface", definition);
            Assert.Contains("IMaze<", definition);
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
            var archiveProcessingService = CreateArchiveProcessingService();
            var listTool = new ListInterfacesTool(listToolLogger, _packageService, archiveProcessingService);

            var packageId = "DimonSmart.MazeGenerator";

            // Step 1: List interfaces in the package
            var result = await listTool.list_interfaces(packageId);
            Assert.NotNull(result);
            Assert.NotEmpty(result.Interfaces);

            // Step 2: Get definition of one of the interfaces
            var mazeInterface = result.Interfaces.FirstOrDefault(i => i.Name.StartsWith("IMaze"));
            if (mazeInterface != null)
            {
                var definition = await _defTool.get_interface_definition(
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
        public async Task GetInterfaceDefinition_WithFullName_ReturnsDefinition()
        {
            var packageId = "DimonSmart.MazeGenerator";
            var fullICellInterfaceName = "DimonSmart.MazeGenerator.ICell";
            var version = await _packageService.GetLatestVersion(packageId);

            var definition = await _defTool.get_interface_definition(packageId, fullICellInterfaceName, version);

            // Assert
            Assert.NotNull(definition);
            Assert.Contains("interface", definition);
            Assert.Contains("ICell", definition);
            Assert.DoesNotContain("not found in package", definition);

            TestOutput.WriteLine("\n========== TEST OUTPUT: ICell INTERFACE DEFINITION (FULL NAME) ==========");
            TestOutput.WriteLine(definition);
            TestOutput.WriteLine("=======================================================================\n");
        }

        [Fact]
        public async Task GetInterfaceDefinition_WithGenericFullName_ReturnsFormattedDefinition()
        {
            var packageId = "DimonSmart.MazeGenerator";
            var fullGenericMazeInterfaceName = "DimonSmart.MazeGenerator.IMaze";
            var version = await _packageService.GetLatestVersion(packageId);

            var definition = await _defTool.get_interface_definition(packageId, fullGenericMazeInterfaceName, version);

            Assert.NotNull(definition);
            Assert.Contains("interface", definition);
            Assert.Contains("IMaze<", definition);
            Assert.DoesNotContain("not found in package", definition);

            TestOutput.WriteLine("\n========== TEST OUTPUT: IMaze INTERFACE DEFINITION (FULL NAME) ==========");
            TestOutput.WriteLine(definition);
            TestOutput.WriteLine("======================================================================\n");
        }
    }
}
