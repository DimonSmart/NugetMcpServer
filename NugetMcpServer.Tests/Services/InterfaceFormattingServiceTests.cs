using System.Collections;
using System.Reflection;

using NugetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Services;

public class InterfaceFormattingServiceTests : TestBase
{
    private readonly InterfaceFormattingService _formattingService;

    public InterfaceFormattingServiceTests(ITestOutputHelper testOutput) : base(testOutput) => _formattingService = new InterfaceFormattingService();

    [Fact]
    public void FormatInterfaceDefinition_WithSimpleInterface_ReturnsFormattedCode()
    {
        // Use a simple interface that's part of the framework
        var interfaceType = typeof(IDisposable);
        var assemblyName = "System.Private.CoreLib";

        // Format the interface
        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        // Assert
        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# INTERFACE FROM {assemblyName} */", formattedCode);
        Assert.Contains("public interface IDisposable", formattedCode);
        Assert.Contains("void Dispose()", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED INTERFACE ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("=====================================================\n");
    }        // Test interface for generic interface formatting
    public interface IMockGeneric<T>
    {
        T GetValue();
        void SetValue(T value);
    }
    [Fact]
    public void FormatInterfaceDefinition_WithGenericInterface_ReturnsFormattedCode()
    {
        // Use our mock generic interface to test formatting of generic interfaces
        var interfaceType = typeof(IMockGeneric<string>);
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        // Format the interface
        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        // Log the output first to see what's actually there
        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED GENERIC INTERFACE ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("===========================================================\n");

        // Assert
        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# INTERFACE FROM {assemblyName} */", formattedCode);
        Assert.Contains("public interface IMockGeneric<string>", formattedCode);
        Assert.Contains("string GetValue()", formattedCode);
        Assert.Contains("void SetValue(string value)", formattedCode);
    }

    [Fact]
    public void FormatInterfaceDefinition_WithDifferentGenericType_ReturnsFormattedCode()
    {
        // Use our mock generic interface with a different type parameter
        var interfaceType = typeof(IMockGeneric<int>);
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        // Format the interface
        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        // Log the output
        TestOutput.WriteLine("\n========== TEST OUTPUT: INT GENERIC INTERFACE ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("=====================================================\n");

        // Assert
        Assert.NotNull(formattedCode);
        Assert.Contains("public interface IMockGeneric<int>", formattedCode);
        Assert.Contains("int GetValue()", formattedCode);
        Assert.Contains("void SetValue(int value)", formattedCode);
    }

    [Fact]
    public void FormatInterfaceDefinition_WithInterfaceHavingProperties_ReturnsFormattedCode()
    {
        // Use ICollection instead of ICollection<T> to avoid generic interface issues
        var interfaceType = typeof(ICollection);
        var assemblyName = "System.Private.CoreLib";

        // Format the interface
        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        // Assert
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
        // Test that the service can format interfaces found by full name
        var interfaceType = typeof(IDisposable);
        var assemblyName = "System.Private.CoreLib";

        // Format the interface (the service itself doesn't do the lookup, 
        // but this validates the formatting works regardless of how the interface was found)
        var formattedCode = _formattingService.FormatInterfaceDefinition(interfaceType, assemblyName);

        // Assert
        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# INTERFACE FROM {assemblyName} */", formattedCode);
        Assert.Contains("public interface IDisposable", formattedCode);
        Assert.Contains("void Dispose()", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: INTERFACE FROM FULL NAME LOOKUP ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("================================================================\n");
    }
}
