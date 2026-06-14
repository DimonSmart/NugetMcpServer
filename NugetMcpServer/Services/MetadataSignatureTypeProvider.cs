using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;

namespace NuGetMcpServer.Services;

internal readonly record struct GenericContext(
    IReadOnlyList<string> TypeParameters,
    IReadOnlyList<string> MethodParameters);

internal sealed class MetadataSignatureTypeProvider
    : ISignatureTypeProvider<string, GenericContext>
{
    public string GetArrayType(string elementType, ArrayShape shape)
    {
        if (shape.Rank == 1)
        {
            return $"{elementType}[]";
        }

        return $"{elementType}[{new string(',', Math.Max(0, shape.Rank - 1))}]";
    }

    public string GetByReferenceType(string elementType) => $"{elementType}&";

    public string GetFunctionPointerType(MethodSignature<string> signature) => "delegate*";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        var tick = genericType.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
        {
            genericType = genericType[..tick];
        }

        return $"{genericType}<{string.Join(", ", typeArguments)}>";
    }

    public string GetGenericMethodParameter(GenericContext genericContext, int index)
    {
        return genericContext.MethodParameters != null &&
            index >= 0 &&
            index < genericContext.MethodParameters.Count
            ? genericContext.MethodParameters[index]
            : $"!!{index}";
    }

    public string GetGenericTypeParameter(GenericContext genericContext, int index)
    {
        return genericContext.TypeParameters != null &&
            index >= 0 &&
            index < genericContext.TypeParameters.Count
            ? genericContext.TypeParameters[index]
            : $"!{index}";
    }

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetPinnedType(string elementType) => elementType;

    public string GetPointerType(string elementType) => $"{elementType}*";

    public string GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return typeCode switch
        {
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Byte => "byte",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.Double => "double",
            PrimitiveTypeCode.Int16 => "short",
            PrimitiveTypeCode.Int32 => "int",
            PrimitiveTypeCode.Int64 => "long",
            PrimitiveTypeCode.IntPtr => "nint",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.SByte => "sbyte",
            PrimitiveTypeCode.Single => "float",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.TypedReference => "TypedReference",
            PrimitiveTypeCode.UInt16 => "ushort",
            PrimitiveTypeCode.UInt32 => "uint",
            PrimitiveTypeCode.UInt64 => "ulong",
            PrimitiveTypeCode.UIntPtr => "nuint",
            PrimitiveTypeCode.Void => "void",
            _ => typeCode.ToString()
        };
    }

    public string GetSZArrayType(string elementType) => $"{elementType}[]";

    public string GetTypeFromDefinition(MetadataReader metadataReader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var definition = metadataReader.GetTypeDefinition(handle);
        return JoinName(
            metadataReader.GetString(definition.Namespace),
            metadataReader.GetString(definition.Name));
    }

    public string GetTypeFromReference(MetadataReader metadataReader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var reference = metadataReader.GetTypeReference(handle);
        var name = metadataReader.GetString(reference.Name);
        var ns = metadataReader.GetString(reference.Namespace);

        if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var declaring = GetTypeFromReference(metadataReader, (TypeReferenceHandle)reference.ResolutionScope, rawTypeKind);
            return $"{declaring}.{name}";
        }

        return JoinName(ns, name);
    }

    public string GetTypeFromSpecification(MetadataReader metadataReader, GenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        var specification = metadataReader.GetTypeSpecification(handle);
        return specification.DecodeSignature(this, genericContext);
    }

    private static string JoinName(string ns, string name)
    {
        return string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
    }
}
