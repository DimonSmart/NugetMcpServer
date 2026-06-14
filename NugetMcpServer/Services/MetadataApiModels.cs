using System.Collections.Generic;

namespace NuGetMcpServer.Services;

public sealed record PackageApiModel
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyList<ApiAssemblyModel> Assemblies { get; init; }
}

public sealed record ApiAssemblyModel
{
    public required string FileName { get; init; }
    public required string PackagePath { get; init; }
    public required string TargetFramework { get; init; }
    public required IReadOnlyList<ApiTypeModel> Types { get; init; }
}

public sealed record ApiTypeModel
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Namespace { get; init; }
    public required ApiTypeKind Kind { get; init; }
    public required bool IsPublic { get; init; }
    public required bool IsNestedPublic { get; init; }
    public required bool IsStatic { get; init; }
    public required bool IsAbstract { get; init; }
    public required bool IsSealed { get; init; }
    public required string AssemblyName { get; init; }
    public string? BaseType { get; init; }
    public IReadOnlyList<string> Interfaces { get; init; } = [];
    public IReadOnlyList<ApiGenericParameterModel> GenericParameters { get; init; } = [];
    public IReadOnlyList<ApiConstructorModel> Constructors { get; init; } = [];
    public IReadOnlyList<ApiMethodModel> Methods { get; init; } = [];
    public IReadOnlyList<ApiPropertyModel> Properties { get; init; } = [];
    public IReadOnlyList<ApiFieldModel> Fields { get; init; } = [];
    public IReadOnlyList<ApiEventModel> Events { get; init; } = [];
    public IReadOnlyList<ApiEnumValueModel> EnumValues { get; init; } = [];
    public string? EnumUnderlyingType { get; init; }
}

public enum ApiTypeKind
{
    Class,
    StaticClass,
    AbstractClass,
    SealedClass,
    Struct,
    Interface,
    Enum,
    Delegate,
    RecordClass,
    RecordStruct
}

public sealed record ApiGenericParameterModel
{
    public required string Name { get; init; }
    public required int Index { get; init; }
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public bool HasReferenceTypeConstraint { get; init; }
    public bool HasValueTypeConstraint { get; init; }
    public bool HasDefaultConstructorConstraint { get; init; }
}

public sealed record ApiParameterModel
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsRef { get; init; }
    public bool IsOut { get; init; }
    public bool IsIn { get; init; }
    public bool IsParams { get; init; }
    public bool IsOptional { get; init; }
    public string? DefaultValue { get; init; }
}

public sealed record ApiMethodModel
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public required IReadOnlyList<ApiParameterModel> Parameters { get; init; }
    public IReadOnlyList<ApiGenericParameterModel> GenericParameters { get; init; } = [];
    public bool IsStatic { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }
    public bool IsExtension { get; init; }
    public bool IsObsolete { get; init; }
    public string Identity { get; init; } = string.Empty;
}

public sealed record ApiConstructorModel
{
    public required IReadOnlyList<ApiParameterModel> Parameters { get; init; }
}

public sealed record ApiPropertyModel
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required IReadOnlyList<ApiParameterModel> IndexParameters { get; init; }
    public bool HasPublicGetter { get; init; }
    public bool HasPublicSetter { get; init; }
    public bool IsStatic { get; init; }
    public bool IsRequired { get; init; }
    public bool IsInitOnly { get; init; }
    public string Identity { get; init; } = string.Empty;
}

public sealed record ApiFieldModel
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsConst { get; init; }
    public bool IsStatic { get; init; }
    public bool IsReadOnly { get; init; }
    public string? LiteralValue { get; init; }
}

public sealed record ApiEventModel
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsStatic { get; init; }
}

public sealed record ApiEnumValueModel
{
    public required string Name { get; init; }
    public required string Value { get; init; }
}

public sealed record PackageAssemblyFile
{
    public required string PackagePath { get; init; }
    public required string FileName { get; init; }
    public required string TargetFramework { get; init; }
    public required byte[] Bytes { get; init; }
}

public sealed record LoadedPackageMetadata
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
    public required PackageInfo PackageInfo { get; init; }
    public required PackageApiModel Api { get; init; }
}
