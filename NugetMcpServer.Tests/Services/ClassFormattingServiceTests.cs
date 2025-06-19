using NugetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Services;

public class ClassFormattingServiceTests(ITestOutputHelper testOutput) : TestBase(testOutput)
{
    private readonly ClassFormattingService _formattingService = new(); [Fact]
    public void FormatClassDefinition_WithSimpleClass_ReturnsFormattedCode()
    {
        var classType = typeof(string);
        var assemblyName = "System.Private.CoreLib";

        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# CLASS FROM {assemblyName} */", formattedCode);
        Assert.Contains("public sealed class string", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED CLASS ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("================================================\n");
    }

    [Fact]
    public void FormatClassDefinition_WithStaticClass_ReturnsFormattedCode()
    {
        var classType = typeof(Console);
        var assemblyName = "System.Console";

        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# CLASS FROM {assemblyName} */", formattedCode);
        Assert.Contains("public static class Console", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED STATIC CLASS ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("======================================================\n");
    }

    [Fact]
    public void FormatClassDefinition_WithAbstractClass_ReturnsFormattedCode()
    {
        var classType = typeof(System.IO.Stream);
        var assemblyName = "System.IO";

        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# CLASS FROM {assemblyName} */", formattedCode);
        Assert.Contains("public abstract class Stream", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED ABSTRACT CLASS ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("========================================================\n");
    }

    // Test class for generic class formatting
    public class MockGeneric<T>
    {
        public T Value { get; set; } = default!;
        public static string StaticProperty { get; set; } = string.Empty;
        public const int CONSTANT_VALUE = 42;
        public static readonly int ReadonlyValue = 100;

        public T GetValue() => Value;
        public void SetValue(T value) => Value = value;
        public static void StaticMethod() { }
    }

    [Fact]
    public void FormatClassDefinition_WithGenericClass_ReturnsFormattedCode()
    {
        var classType = typeof(MockGeneric<string>);
        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!;

        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED GENERIC CLASS ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("========================================================\n");

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# CLASS FROM {assemblyName} */", formattedCode);
        Assert.Contains("public class MockGeneric<string>", formattedCode);
        Assert.Contains("CONSTANT_VALUE = 42", formattedCode);
        Assert.Contains("static readonly", formattedCode);
        Assert.Contains("string GetValue()", formattedCode);
        Assert.Contains("void SetValue(string value)", formattedCode);
    }

    [Fact]
    public void FormatClassDefinition_WithClassHavingConstants_ReturnsFormattedCode()
    {
        var classType = typeof(int);
        var assemblyName = "System.Private.CoreLib";

        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# CLASS FROM {assemblyName} */", formattedCode);
        Assert.Contains("public struct int", formattedCode);

        TestOutput.WriteLine("\n========== TEST OUTPUT: CLASS WITH CONSTANTS ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("====================================================\n");
    }
}
