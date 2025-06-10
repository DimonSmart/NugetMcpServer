using System;

using NuGetMcpServer.Services;

using Xunit;

namespace NuGetMcpServer.Tests.Services;

public class EnumFormattingServiceTests
{
    private readonly EnumFormattingService _service = new(); [Fact]
    public void FormatEnumDefinition_Should_Format_SimpleEnum()
    {
        // Arrange
        var enumType = typeof(TestEnum);

        // Act
        var result = _service.FormatEnumDefinition(enumType);

        // Assert
        Assert.Contains("public enum TestEnum", result);
        Assert.Contains("First = 0", result);
        Assert.Contains("Second = 1", result);
        Assert.Contains("Third = 2", result);
    }

    [Fact]
    public void FormatEnumDefinition_Should_Format_EnumWithExplicitValues()
    {
        // Arrange
        var enumType = typeof(TestEnumWithValues);

        // Act
        var result = _service.FormatEnumDefinition(enumType);

        // Assert
        Assert.Contains("public enum TestEnumWithValues", result);
        Assert.Contains("None = 0", result);
        Assert.Contains("First = 10", result);
        Assert.Contains("Second = 20", result);
        Assert.Contains("Third = 30", result);
    }

    [Fact]
    public void FormatEnumDefinition_Should_Format_EnumWithDifferentUnderlyingType()
    {
        // Arrange
        var enumType = typeof(TestULongEnum);

        // Act
        var result = _service.FormatEnumDefinition(enumType);

        // Assert
        Assert.Contains("public enum TestULongEnum : ulong", result);
        Assert.Contains("Value1 = 1UL", result);
        Assert.Contains("Value2 = 9223372036854775808UL", result); // 2^63
    }

    [Fact]
    public void FormatEnumDefinition_Should_ThrowArgumentException_WhenTypeIsNotEnum()
    {
        // Arrange
        var nonEnumType = typeof(string);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _service.FormatEnumDefinition(nonEnumType));
        Assert.Contains("is not an enum", ex.Message);
    }

    // Test enums
    public enum TestEnum
    {
        First,
        Second,
        Third
    }

    public enum TestEnumWithValues
    {
        None = 0,
        First = 10,
        Second = 20,
        Third = 30
    }

    public enum TestULongEnum : ulong
    {
        Value1 = 1,
        Value2 = 9223372036854775808 // 2^63
    }
}
