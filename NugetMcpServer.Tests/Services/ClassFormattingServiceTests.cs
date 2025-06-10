using NugetMcpServer.Tests.Helpers;

using NuGetMcpServer.Services;

using Xunit.Abstractions;

namespace NugetMcpServer.Tests.Services;

public class ClassFormattingServiceTests : TestBase
{
    private readonly ClassFormattingService _formattingService;

    public ClassFormattingServiceTests(ITestOutputHelper testOutput) : base(testOutput) => _formattingService = new ClassFormattingService();

    [Fact]
    public void FormatClassDefinition_WithSimpleClass_ReturnsFormattedCode()
    {
        // Use a simple class that's part of the framework
        var classType = typeof(string);
        var assemblyName = "System.Private.CoreLib";

        // Format the class
        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        // Assert
        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# CLASS FROM {assemblyName} */", formattedCode);
        Assert.Contains("public sealed class string", formattedCode); // string is the C# alias for String

        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED CLASS ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("================================================\n");
    }

    [Fact]
    public void FormatClassDefinition_WithStaticClass_ReturnsFormattedCode()
    {
        // Use a static class like Console
        var classType = typeof(Console);
        var assemblyName = "System.Console";

        // Format the class
        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        // Assert
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
        // Use an abstract class like Stream
        var classType = typeof(System.IO.Stream);
        var assemblyName = "System.IO";

        // Format the class
        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        // Assert
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
        // Use our mock generic class to test formatting of generic classes
        var classType = typeof(MockGeneric<string>);
        var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!;

        // Format the class
        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        // Log the output first to see what's actually there
        TestOutput.WriteLine("\n========== TEST OUTPUT: FORMATTED GENERIC CLASS ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("========================================================\n");

        // Assert
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
        // Create a simple test class with constants (since we can't easily find one in the framework)
        var classType = typeof(int); // int has MaxValue, MinValue constants
        var assemblyName = "System.Private.CoreLib";

        // Format the class
        var formattedCode = _formattingService.FormatClassDefinition(classType, assemblyName);

        // Assert
        Assert.NotNull(formattedCode);
        Assert.Contains($"/* C# CLASS FROM {assemblyName} */", formattedCode);
        Assert.Contains("public struct int", formattedCode); // int is actually a struct, not a class

        TestOutput.WriteLine("\n========== TEST OUTPUT: CLASS WITH CONSTANTS ==========");
        TestOutput.WriteLine(formattedCode);
        TestOutput.WriteLine("====================================================\n");
    }
}
