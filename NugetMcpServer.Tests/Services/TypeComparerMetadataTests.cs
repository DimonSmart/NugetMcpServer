using NuGetMcpServer.Models;
using NuGetMcpServer.Services;
using Xunit;

namespace NuGetMcpServer.Tests.Services;

public sealed class TypeComparerMetadataTests
{
    [Fact]
    public void CompareTypes_UsesMethodIdentityWithParameterTypes()
    {
        var oldType = CreateType(methods:
        [
            new ApiMethodModel
            {
                Name = "Find",
                ReturnType = "string",
                Parameters = [new ApiParameterModel { Name = "id", Type = "int" }],
                Identity = "M:Find`0(int)"
            }
        ]);
        var newType = CreateType(methods:
        [
            new ApiMethodModel
            {
                Name = "Find",
                ReturnType = "string",
                Parameters = [new ApiParameterModel { Name = "id", Type = "int" }],
                Identity = "M:Find`0(int)"
            },
            new ApiMethodModel
            {
                Name = "Find",
                ReturnType = "string",
                Parameters = [new ApiParameterModel { Name = "name", Type = "string" }],
                Identity = "M:Find`0(string)"
            }
        ]);

        var changes = new TypeComparer().CompareTypes(oldType, newType);

        Assert.Contains(changes, change =>
            change.Category == ChangeCategory.MemberAdded &&
            change.MemberName == "Find" &&
            change.To?.Contains("string") == true);
        Assert.DoesNotContain(changes, change => change.Category == ChangeCategory.MemberRemoved);
    }

    [Fact]
    public void CompareTypes_DetectsPropertyTypeChanges()
    {
        var oldType = CreateType(properties:
        [
            new ApiPropertyModel
            {
                Name = "Value",
                Type = "string",
                IndexParameters = [],
                HasPublicGetter = true,
                Identity = "P:Value()"
            }
        ]);
        var newType = oldType with
        {
            Properties =
            [
                new ApiPropertyModel
                {
                    Name = "Value",
                    Type = "int",
                    IndexParameters = [],
                    HasPublicGetter = true,
                    Identity = "P:Value()"
                }
            ]
        };

        var changes = new TypeComparer().CompareTypes(oldType, newType);

        Assert.Contains(changes, change =>
            change.Category == ChangeCategory.MemberTypeChanged &&
            change.MemberName == "Value" &&
            change.From == "string" &&
            change.To == "int");
    }

    [Fact]
    public void CompareTypes_DetectsEnumValueChanges()
    {
        var oldType = CreateType(ApiTypeKind.Enum) with
        {
            EnumValues =
            [
                new ApiEnumValueModel { Name = "A", Value = "0" },
                new ApiEnumValueModel { Name = "B", Value = "1" }
            ]
        };
        var newType = CreateType(ApiTypeKind.Enum) with
        {
            EnumValues =
            [
                new ApiEnumValueModel { Name = "B", Value = "1" },
                new ApiEnumValueModel { Name = "C", Value = "2" }
            ]
        };

        var changes = new TypeComparer().CompareTypes(oldType, newType);

        Assert.Contains(changes, change => change.Category == ChangeCategory.EnumValueRemoved && change.MemberName == "A");
        Assert.Contains(changes, change => change.Category == ChangeCategory.EnumValueAdded && change.MemberName == "C");
    }

    [Fact]
    public void CompareTypes_DetectsGenericConstraintChanges()
    {
        var oldType = CreateType() with
        {
            GenericParameters =
            [
                new ApiGenericParameterModel
                {
                    Name = "T",
                    Index = 0,
                    HasReferenceTypeConstraint = true
                }
            ]
        };
        var newType = oldType with
        {
            GenericParameters =
            [
                new ApiGenericParameterModel
                {
                    Name = "T",
                    Index = 0,
                    HasDefaultConstructorConstraint = true
                }
            ]
        };

        var changes = new TypeComparer().CompareTypes(oldType, newType);

        Assert.Contains(changes, change => change.Category == ChangeCategory.GenericParametersChanged);
    }

    private static ApiTypeModel CreateType(
        ApiTypeKind kind = ApiTypeKind.Class,
        IReadOnlyList<ApiMethodModel>? methods = null,
        IReadOnlyList<ApiPropertyModel>? properties = null)
    {
        return new ApiTypeModel
        {
            Name = "Sample",
            FullName = "Tests.Sample",
            Namespace = "Tests",
            Kind = kind,
            IsPublic = true,
            IsNestedPublic = false,
            IsStatic = false,
            IsAbstract = false,
            IsSealed = false,
            AssemblyName = "Tests.dll",
            Methods = methods ?? [],
            Properties = properties ?? []
        };
    }
}
