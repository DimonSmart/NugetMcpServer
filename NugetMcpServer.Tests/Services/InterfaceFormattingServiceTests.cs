using System.Collections;
using System.Reflection;

using NugetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Services;

public class InterfaceFormattingServiceTests : TestBase
{
    private readonly InterfaceFormattingService _formattingService;

    public InterfaceFormattingServiceTests(ITestOutputHelper testOutput) : base(testOutput) => _formattingService = new InterfaceFormattingService(); [Fact]
    public void FormatInterfaceDefinition_WithSimpleInterface_ReturnsFormattedCode()
    {
        var interfaceType = typeof(IDisposable);
        var assemblyName = "System.Private.CoreLib";

        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# INTERFACE FROM {assemblyName} */", formattedCode);
        Assert.Contains("public interface IDisposable", formattedCode);
        Assert.Contains("void Dispose()", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED INTERFACE ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("=====================================================\n");
    }
    public interface IMockGeneric<T>
    {
        T GetValue();
        void SetValue(T value);
    }

    [Fact]
    public void FormatInterfaceDefinition_WithGenericInterface_ReturnsFormattedCode()
    {
        var interfaceType = typeof(IMockGeneric<string>);
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED GENERIC INTERFACE ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("===========================================================\n");

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# INTERFACE FROM {assemblyName} */", formattedCode);
        Assert.Contains("public interface IMockGeneric<string>", formattedCode);
        Assert.Contains("string GetValue()", formattedCode);
        Assert.Contains("void SetValue(string value)", formattedCode);
    }

    [Fact]
    public void FormatInterfaceDefinition_WithDifferentGenericType_ReturnsFormattedCode()
    {
        var interfaceType = typeof(IMockGeneric<int>);
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        TestOutput.WriteLine("\n========== TEST OUTPUT: INT GENERIC INTERFACE ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("=====================================================\n");

        Assert.NotNull(formattedCode);
        Assert.Contains("public interface IMockGeneric<int>", formattedCode);
        Assert.Contains("int GetValue()", formattedCode);
        Assert.Contains("void SetValue(int value)", formattedCode);
    }

    [Fact]
    public void FormatInterfaceDefinition_WithInterfaceHavingProperties_ReturnsFormattedCode()
    {
        var interfaceType = typeof(ICollection);
        var assemblyName = "System.Private.CoreLib";

        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# INTERFACE FROM {assemblyName} */", formattedCode);
        Assert.Contains("public interface ICollection", formattedCode);
        Assert.Contains("int Count { get; }", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: INTERFACE WITH PROPERTIES ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("========================================================\n");
    }

    [Fact]
    public void FormatInterfaceDefinition_WithFullNameMatch_ReturnsFormattedCode()
    {
        var interfaceType = typeof(IDisposable);
        var assemblyName = "System.Private.CoreLib";

        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# INTERFACE FROM {assemblyName} */", formattedCode);
        Assert.Contains("public interface IDisposable", formattedCode);
        Assert.Contains("void Dispose()", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: INTERFACE FROM FULL NAME LOOKUP ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("================================================================\n");
    }
}
