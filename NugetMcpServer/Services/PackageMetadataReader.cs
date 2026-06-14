using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Extensions.Logging;

namespace NuGetMcpServer.Services;

public sealed class PackageMetadataReader(ILogger<PackageMetadataReader> logger)
{
    private static readonly HashSet<string> CompilerGeneratedTypeNames = new(StringComparer.Ordinal)
    {
        "PrivateImplementationDetails"
    };

    private readonly ILogger<PackageMetadataReader> _logger = logger;

    public PackageApiModel ReadPackageApi(
        string packageId,
        string version,
        IReadOnlyList<PackageAssemblyFile> assemblies)
    {
        var apiAssemblies = new List<ApiAssemblyModel>();

        foreach (var assembly in assemblies.OrderBy(a => a.PackagePath, StringComparer.OrdinalIgnoreCase))
        {
            var model = TryReadAssembly(assembly);
            if (model != null)
            {
                apiAssemblies.Add(model);
            }
        }

        return new PackageApiModel
        {
            PackageId = packageId,
            Version = version,
            Assemblies = apiAssemblies
        };
    }

    private ApiAssemblyModel? TryReadAssembly(PackageAssemblyFile assembly)
    {
        try
        {
            using var stream = new MemoryStream(assembly.Bytes, writable: false);
            using var peReader = new PEReader(stream);

            if (!peReader.HasMetadata)
            {
                _logger.LogDebug("Skipping non-managed DLL {PackagePath}", assembly.PackagePath);
                return null;
            }

            var reader = peReader.GetMetadataReader();
            var provider = new MetadataSignatureTypeProvider();
            var typeDefinitions = reader.TypeDefinitions
                .Select(handle => ReadType(reader, provider, handle, assembly.FileName))
                .Where(type => type != null)
                .Cast<ApiTypeModel>()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();

            return new ApiAssemblyModel
            {
                FileName = assembly.FileName,
                PackagePath = assembly.PackagePath,
                TargetFramework = assembly.TargetFramework,
                Types = typeDefinitions
            };
        }
        catch (BadImageFormatException ex)
        {
            _logger.LogDebug(ex, "Skipping invalid managed DLL {PackagePath}", assembly.PackagePath);
            return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            _logger.LogDebug(ex, "Skipping unreadable DLL {PackagePath}", assembly.PackagePath);
            return null;
        }
    }

    private static ApiTypeModel? ReadType(
        MetadataReader reader,
        MetadataSignatureTypeProvider provider,
        TypeDefinitionHandle handle,
        string assemblyName)
    {
        var type = reader.GetTypeDefinition(handle);
        var attributes = type.Attributes;
        var isPublic = IsTopLevelPublic(attributes);
        var isNestedPublic = IsNestedPublic(attributes);
        if (!isPublic && !isNestedPublic)
        {
            return null;
        }

        var name = reader.GetString(type.Name);
        var ns = reader.GetString(type.Namespace);
        if (IsCompilerGeneratedType(reader, type, name, ns))
        {
            return null;
        }

        var typeGenericParameters = ReadGenericParameters(reader, provider, type.GetGenericParameters(), default);
        var typeParameterNames = typeGenericParameters.Select(p => p.Name).ToArray();
        var context = new GenericContext(typeParameterNames, []);

        var baseType = DecodeTypeHandle(reader, provider, type.BaseType, context);
        var interfaces = type.GetInterfaceImplementations()
            .Select(interfaceHandle => reader.GetInterfaceImplementation(interfaceHandle))
            .Select(implementation => DecodeTypeHandle(reader, provider, implementation.Interface, context))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToList();

        var methods = new List<ApiMethodModel>();
        var constructors = new List<ApiConstructorModel>();
        foreach (var methodHandle in type.GetMethods())
        {
            var method = reader.GetMethodDefinition(methodHandle);
            if (!method.Attributes.HasFlag(MethodAttributes.Public))
            {
                continue;
            }

            var methodName = reader.GetString(method.Name);
            if (methodName == ".ctor")
            {
                constructors.Add(ReadConstructor(reader, provider, method, context));
                continue;
            }

            if (method.Attributes.HasFlag(MethodAttributes.SpecialName) ||
                IsCompilerGeneratedMember(reader, method.GetCustomAttributes(), methodName))
            {
                continue;
            }

            methods.Add(ReadMethod(reader, provider, method, methodName, context));
        }

        var properties = type.GetProperties()
            .Select(propertyHandle => ReadProperty(reader, provider, propertyHandle, type, context))
            .Where(property => property != null)
            .Cast<ApiPropertyModel>()
            .OrderBy(static property => property.Identity, StringComparer.Ordinal)
            .ToList();

        var events = type.GetEvents()
            .Select(eventHandle => ReadEvent(reader, provider, eventHandle, context))
            .Where(evt => evt != null)
            .Cast<ApiEventModel>()
            .OrderBy(static evt => evt.Name, StringComparer.Ordinal)
            .ToList();

        var fields = type.GetFields()
            .Select(fieldHandle => ReadField(reader, provider, fieldHandle, context))
            .Where(field => field != null)
            .Cast<ApiFieldModel>()
            .OrderBy(static field => field.Name, StringComparer.Ordinal)
            .ToList();

        var enumValues = fields
            .Where(static field => field.IsConst && field.Name != "value__")
            .Select(field => new ApiEnumValueModel
            {
                Name = field.Name,
                Value = field.LiteralValue ?? string.Empty
            })
            .ToList();

        var kind = GetTypeKind(reader, type, attributes, baseType, methods);
        var fullName = string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";

        return new ApiTypeModel
        {
            Name = FormatGenericName(name, typeGenericParameters),
            FullName = FormatGenericFullName(fullName, typeGenericParameters),
            Namespace = ns,
            Kind = kind,
            IsPublic = isPublic,
            IsNestedPublic = isNestedPublic,
            IsStatic = attributes.HasFlag(TypeAttributes.Abstract) && attributes.HasFlag(TypeAttributes.Sealed),
            IsAbstract = attributes.HasFlag(TypeAttributes.Abstract),
            IsSealed = attributes.HasFlag(TypeAttributes.Sealed),
            AssemblyName = assemblyName,
            BaseType = NormalizeBaseType(baseType, kind),
            Interfaces = interfaces,
            GenericParameters = typeGenericParameters,
            Constructors = constructors.OrderBy(static ctor => ParameterIdentity(ctor.Parameters), StringComparer.Ordinal).ToList(),
            Methods = methods.OrderBy(static method => method.Identity, StringComparer.Ordinal).ToList(),
            Properties = properties,
            Fields = fields.Where(field => kind != ApiTypeKind.Enum || field.Name == "value__" || !field.IsConst).ToList(),
            Events = events,
            EnumValues = enumValues,
            EnumUnderlyingType = kind == ApiTypeKind.Enum
                ? fields.FirstOrDefault(static field => field.Name == "value__")?.Type
                : null
        };
    }

    private static bool IsTopLevelPublic(TypeAttributes attributes) =>
        (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;

    private static bool IsNestedPublic(TypeAttributes attributes) =>
        (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic;

    private static bool IsCompilerGeneratedType(MetadataReader reader, TypeDefinition type, string name, string ns)
    {
        if (name.StartsWith('<') ||
            name.Contains("DisplayClass", StringComparison.Ordinal) ||
            name.Contains("AnonymousType", StringComparison.Ordinal) ||
            ns == "System.Runtime.CompilerServices" ||
            CompilerGeneratedTypeNames.Contains(name))
        {
            return true;
        }

        return HasAttribute(reader, type.GetCustomAttributes(), "CompilerGeneratedAttribute");
    }

    private static bool IsCompilerGeneratedMember(MetadataReader reader, CustomAttributeHandleCollection attributes, string name)
    {
        return name.StartsWith('<') || HasAttribute(reader, attributes, "CompilerGeneratedAttribute");
    }

    private static ApiConstructorModel ReadConstructor(
        MetadataReader reader,
        MetadataSignatureTypeProvider provider,
        MethodDefinition method,
        GenericContext typeContext)
    {
        var signature = method.DecodeSignature(provider, typeContext);
        var parameters = ReadParameters(reader, method.GetParameters(), signature.ParameterTypes);
        return new ApiConstructorModel { Parameters = parameters };
    }

    private static ApiMethodModel ReadMethod(
        MetadataReader reader,
        MetadataSignatureTypeProvider provider,
        MethodDefinition method,
        string methodName,
        GenericContext typeContext)
    {
        var genericParameters = ReadGenericParameters(reader, provider, method.GetGenericParameters(), typeContext);
        var methodContext = typeContext with
        {
            MethodParameters = genericParameters.Select(static p => p.Name).ToArray()
        };
        var signature = method.DecodeSignature(provider, methodContext);
        var parameters = ReadParameters(reader, method.GetParameters(), signature.ParameterTypes);
        var name = FormatGenericMethodName(methodName, genericParameters);

        return new ApiMethodModel
        {
            Name = name,
            ReturnType = ApplyNullable(reader, method.GetCustomAttributes(), signature.ReturnType),
            Parameters = parameters,
            GenericParameters = genericParameters,
            IsStatic = method.Attributes.HasFlag(MethodAttributes.Static),
            IsAbstract = method.Attributes.HasFlag(MethodAttributes.Abstract),
            IsVirtual = method.Attributes.HasFlag(MethodAttributes.Virtual),
            IsOverride = method.Attributes.HasFlag(MethodAttributes.Virtual) &&
                         !method.Attributes.HasFlag(MethodAttributes.NewSlot),
            IsExtension = HasAttribute(reader, method.GetCustomAttributes(), "ExtensionAttribute"),
            IsObsolete = HasAttribute(reader, method.GetCustomAttributes(), "ObsoleteAttribute"),
            Identity = MethodIdentity(methodName, genericParameters.Count, parameters)
        };
    }

    private static ApiPropertyModel? ReadProperty(
        MetadataReader reader,
        MetadataSignatureTypeProvider provider,
        PropertyDefinitionHandle handle,
        TypeDefinition declaringType,
        GenericContext context)
    {
        var property = reader.GetPropertyDefinition(handle);
        var accessors = property.GetAccessors();
        var getter = accessors.Getter.IsNil ? default(MethodDefinition?) : reader.GetMethodDefinition(accessors.Getter);
        var setter = accessors.Setter.IsNil ? default(MethodDefinition?) : reader.GetMethodDefinition(accessors.Setter);
        var hasPublicGetter = getter?.Attributes.HasFlag(MethodAttributes.Public) == true;
        var hasPublicSetter = setter?.Attributes.HasFlag(MethodAttributes.Public) == true;
        if (!hasPublicGetter && !hasPublicSetter)
        {
            return null;
        }

        var signature = property.DecodeSignature(provider, context);
        var accessor = getter ?? setter!.Value;
        var indexParameterHandles = accessor.GetParameters()
            .Select(reader.GetParameter)
            .Where(static p => p.SequenceNumber > 0)
            .ToList();
        var indexTypes = signature.ParameterTypes;
        var indexParameters = BuildParameters(reader, indexParameterHandles, indexTypes);
        var name = reader.GetString(property.Name);
        var isStatic = getter?.Attributes.HasFlag(MethodAttributes.Static) == true ||
                       setter?.Attributes.HasFlag(MethodAttributes.Static) == true;

        return new ApiPropertyModel
        {
            Name = name,
            Type = ApplyNullable(reader, property.GetCustomAttributes(), signature.ReturnType),
            IndexParameters = indexParameters,
            HasPublicGetter = hasPublicGetter,
            HasPublicSetter = hasPublicSetter,
            IsStatic = isStatic,
            IsRequired = HasAttribute(reader, property.GetCustomAttributes(), "RequiredMemberAttribute"),
            // TODO: init-only setters require decoding required custom modifiers on the setter return parameter.
            IsInitOnly = false,
            Identity = $"P:{name}({ParameterIdentity(indexParameters)})"
        };
    }

    private static ApiEventModel? ReadEvent(
        MetadataReader reader,
        MetadataSignatureTypeProvider provider,
        EventDefinitionHandle handle,
        GenericContext context)
    {
        var evt = reader.GetEventDefinition(handle);
        var accessors = evt.GetAccessors();
        var add = accessors.Adder.IsNil ? default(MethodDefinition?) : reader.GetMethodDefinition(accessors.Adder);
        var remove = accessors.Remover.IsNil ? default(MethodDefinition?) : reader.GetMethodDefinition(accessors.Remover);
        if (add?.Attributes.HasFlag(MethodAttributes.Public) != true &&
            remove?.Attributes.HasFlag(MethodAttributes.Public) != true)
        {
            return null;
        }

        return new ApiEventModel
        {
            Name = reader.GetString(evt.Name),
            Type = DecodeTypeHandle(reader, provider, evt.Type, context) ?? "object",
            IsStatic = add?.Attributes.HasFlag(MethodAttributes.Static) == true ||
                       remove?.Attributes.HasFlag(MethodAttributes.Static) == true
        };
    }

    private static ApiFieldModel? ReadField(
        MetadataReader reader,
        MetadataSignatureTypeProvider provider,
        FieldDefinitionHandle handle,
        GenericContext context)
    {
        var field = reader.GetFieldDefinition(handle);
        if (!field.Attributes.HasFlag(FieldAttributes.Public))
        {
            return null;
        }

        var isConst = field.Attributes.HasFlag(FieldAttributes.Literal);
        var isStatic = field.Attributes.HasFlag(FieldAttributes.Static);
        var isReadOnly = field.Attributes.HasFlag(FieldAttributes.InitOnly);
        if (!isConst && !(isStatic && isReadOnly))
        {
            return null;
        }

        return new ApiFieldModel
        {
            Name = reader.GetString(field.Name),
            Type = field.DecodeSignature(provider, context),
            IsConst = isConst,
            IsStatic = isStatic,
            IsReadOnly = isReadOnly,
            LiteralValue = isConst ? ReadConstant(reader, field.GetDefaultValue()) : null
        };
    }

    private static IReadOnlyList<ApiGenericParameterModel> ReadGenericParameters(
        MetadataReader reader,
        MetadataSignatureTypeProvider provider,
        GenericParameterHandleCollection handles,
        GenericContext context)
    {
        return handles
            .Select(handle =>
            {
                var parameter = reader.GetGenericParameter(handle);
                var constraints = parameter.GetConstraints()
                    .Select(constraintHandle => reader.GetGenericParameterConstraint(constraintHandle))
                    .Select(constraint => DecodeTypeHandle(reader, provider, constraint.Type, context))
                    .Where(static constraint => !string.IsNullOrWhiteSpace(constraint))
                    .Cast<string>()
                    .ToList();

                return new ApiGenericParameterModel
                {
                    Name = reader.GetString(parameter.Name),
                    Index = parameter.Index,
                    Constraints = constraints,
                    HasReferenceTypeConstraint = parameter.Attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint),
                    HasValueTypeConstraint = parameter.Attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint),
                    HasDefaultConstructorConstraint = parameter.Attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
                };
            })
            .OrderBy(static parameter => parameter.Index)
            .ToList();
    }

    private static IReadOnlyList<ApiParameterModel> ReadParameters(
        MetadataReader reader,
        ParameterHandleCollection parameterHandles,
        IReadOnlyList<string> parameterTypes)
    {
        var parameters = parameterHandles
            .Select(reader.GetParameter)
            .Where(static parameter => parameter.SequenceNumber > 0)
            .OrderBy(static parameter => parameter.SequenceNumber)
            .ToList();

        return BuildParameters(reader, parameters, parameterTypes);
    }

    private static IReadOnlyList<ApiParameterModel> BuildParameters(
        MetadataReader reader,
        IReadOnlyList<Parameter> parameters,
        IReadOnlyList<string> parameterTypes)
    {
        var result = new List<ApiParameterModel>();
        for (var i = 0; i < parameters.Count && i < parameterTypes.Count; i++)
        {
            var parameter = parameters[i];
            var type = ApplyNullable(reader, parameter.GetCustomAttributes(), parameterTypes[i]);
            var isByRef = type.EndsWith('&');
            if (isByRef)
            {
                type = type[..^1];
            }

            result.Add(new ApiParameterModel
            {
                Name = reader.GetString(parameter.Name),
                Type = type,
                IsRef = isByRef && !parameter.Attributes.HasFlag(ParameterAttributes.Out),
                IsOut = parameter.Attributes.HasFlag(ParameterAttributes.Out),
                IsIn = parameter.Attributes.HasFlag(ParameterAttributes.In),
                IsParams = HasAttribute(reader, parameter.GetCustomAttributes(), "ParamArrayAttribute"),
                IsOptional = parameter.Attributes.HasFlag(ParameterAttributes.Optional),
                DefaultValue = ReadConstant(reader, parameter.GetDefaultValue())
            });
        }

        return result;
    }

    private static string? DecodeTypeHandle(
        MetadataReader reader,
        MetadataSignatureTypeProvider provider,
        EntityHandle handle,
        GenericContext context)
    {
        if (handle.IsNil)
        {
            return null;
        }

        return handle.Kind switch
        {
            HandleKind.TypeDefinition => provider.GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
            HandleKind.TypeReference => provider.GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
            HandleKind.TypeSpecification => provider.GetTypeFromSpecification(reader, context, (TypeSpecificationHandle)handle, 0),
            _ => null
        };
    }

    private static ApiTypeKind GetTypeKind(
        MetadataReader reader,
        TypeDefinition type,
        TypeAttributes attributes,
        string? baseType,
        IReadOnlyList<ApiMethodModel> methods)
    {
        if (baseType == "System.Enum")
        {
            return ApiTypeKind.Enum;
        }

        if (baseType == "System.MulticastDelegate" || baseType == "System.Delegate")
        {
            return ApiTypeKind.Delegate;
        }

        if (attributes.HasFlag(TypeAttributes.Interface))
        {
            return ApiTypeKind.Interface;
        }

        if (baseType == "System.ValueType")
        {
            // Record detection is intentionally best-effort. Metadata has no single record bit.
            return HasAttribute(reader, type.GetCustomAttributes(), "IsReadOnlyAttribute") &&
                   methods.Any(static method => method.Name == "PrintMembers")
                ? ApiTypeKind.RecordStruct
                : ApiTypeKind.Struct;
        }

        if (attributes.HasFlag(TypeAttributes.Abstract) && attributes.HasFlag(TypeAttributes.Sealed))
        {
            return ApiTypeKind.StaticClass;
        }

        if (methods.Any(static method => method.Name == "PrintMembers"))
        {
            return ApiTypeKind.RecordClass;
        }

        if (attributes.HasFlag(TypeAttributes.Abstract))
        {
            return ApiTypeKind.AbstractClass;
        }

        if (attributes.HasFlag(TypeAttributes.Sealed))
        {
            return ApiTypeKind.SealedClass;
        }

        return ApiTypeKind.Class;
    }

    private static string? NormalizeBaseType(string? baseType, ApiTypeKind kind)
    {
        if (kind is ApiTypeKind.Class or ApiTypeKind.StaticClass or ApiTypeKind.AbstractClass or ApiTypeKind.SealedClass or ApiTypeKind.RecordClass &&
            baseType is "System.Object")
        {
            return null;
        }

        if (kind is ApiTypeKind.Struct or ApiTypeKind.RecordStruct && baseType is "System.ValueType")
        {
            return null;
        }

        if (kind is ApiTypeKind.Enum or ApiTypeKind.Delegate)
        {
            return null;
        }

        return baseType;
    }

    private static string FormatGenericName(string name, IReadOnlyList<ApiGenericParameterModel> genericParameters)
    {
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
        {
            name = name[..tick];
        }

        return genericParameters.Count == 0
            ? name
            : $"{name}<{string.Join(", ", genericParameters.Select(static p => p.Name))}>";
    }

    private static string FormatGenericFullName(string fullName, IReadOnlyList<ApiGenericParameterModel> genericParameters)
    {
        var tick = fullName.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
        {
            fullName = fullName[..tick];
        }

        return genericParameters.Count == 0
            ? fullName
            : $"{fullName}<{string.Join(", ", genericParameters.Select(static p => p.Name))}>";
    }

    private static string FormatGenericMethodName(string name, IReadOnlyList<ApiGenericParameterModel> genericParameters)
    {
        return genericParameters.Count == 0
            ? name
            : $"{name}<{string.Join(", ", genericParameters.Select(static p => p.Name))}>";
    }

    private static string MethodIdentity(string name, int genericArity, IReadOnlyList<ApiParameterModel> parameters)
    {
        return $"M:{name}`{genericArity}({ParameterIdentity(parameters)})";
    }

    private static string ParameterIdentity(IReadOnlyList<ApiParameterModel> parameters)
    {
        return string.Join(",", parameters.Select(static parameter => parameter.Type));
    }

    private static string? ReadConstant(MetadataReader reader, ConstantHandle handle)
    {
        if (handle.IsNil)
        {
            return null;
        }

        var constant = reader.GetConstant(handle);
        var readerValue = reader.GetBlobReader(constant.Value);
        object? value = constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => readerValue.ReadBoolean(),
            ConstantTypeCode.Byte => readerValue.ReadByte(),
            ConstantTypeCode.Char => $"'{readerValue.ReadChar()}'",
            ConstantTypeCode.Double => readerValue.ReadDouble().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int16 => readerValue.ReadInt16().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int32 => readerValue.ReadInt32().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Int64 => readerValue.ReadInt64().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.SByte => readerValue.ReadSByte().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.Single => readerValue.ReadSingle().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.String => QuoteString(readerValue.ReadUTF16(readerValue.Length)),
            ConstantTypeCode.UInt16 => readerValue.ReadUInt16().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt32 => readerValue.ReadUInt32().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.UInt64 => readerValue.ReadUInt64().ToString(CultureInfo.InvariantCulture),
            ConstantTypeCode.NullReference => "null",
            _ => null
        };

        return value?.ToString();
    }

    private static string QuoteString(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static bool HasAttribute(MetadataReader reader, CustomAttributeHandleCollection attributes, string attributeName)
    {
        return attributes.Any(attributeHandle => GetAttributeTypeName(reader, attributeHandle).EndsWith(attributeName, StringComparison.Ordinal));
    }

    private static string GetAttributeTypeName(MetadataReader reader, CustomAttributeHandle attributeHandle)
    {
        var attribute = reader.GetCustomAttribute(attributeHandle);
        return attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => GetMemberReferenceParentName(reader, (MemberReferenceHandle)attribute.Constructor),
            HandleKind.MethodDefinition => GetMethodDefinitionDeclaringTypeName(reader, (MethodDefinitionHandle)attribute.Constructor),
            _ => string.Empty
        };
    }

    private static string GetMemberReferenceParentName(MetadataReader reader, MemberReferenceHandle handle)
    {
        var member = reader.GetMemberReference(handle);
        return member.Parent.Kind switch
        {
            HandleKind.TypeReference => new MetadataSignatureTypeProvider().GetTypeFromReference(reader, (TypeReferenceHandle)member.Parent, 0),
            HandleKind.TypeDefinition => new MetadataSignatureTypeProvider().GetTypeFromDefinition(reader, (TypeDefinitionHandle)member.Parent, 0),
            _ => string.Empty
        };
    }

    private static string GetMethodDefinitionDeclaringTypeName(MetadataReader reader, MethodDefinitionHandle handle)
    {
        var declaringType = reader.GetMethodDefinition(handle).GetDeclaringType();
        return declaringType.IsNil
            ? string.Empty
            : new MetadataSignatureTypeProvider().GetTypeFromDefinition(reader, declaringType, 0);
    }

    private static string ApplyNullable(MetadataReader reader, CustomAttributeHandleCollection attributes, string type)
    {
        if (IsNonNullableValueType(type) || type.EndsWith('?') || type.EndsWith('*') || type == "void")
        {
            return type;
        }

        foreach (var attributeHandle in attributes)
        {
            if (!GetAttributeTypeName(reader, attributeHandle).EndsWith("NullableAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            var attribute = reader.GetCustomAttribute(attributeHandle);
            var blob = reader.GetBlobReader(attribute.Value);
            _ = blob.ReadUInt16();
            if (blob.RemainingBytes > 0 && blob.ReadByte() == 2)
            {
                return ApplyNullableToType(type);
            }
        }

        return type;
    }

    private static string ApplyNullableToType(string type)
    {
        if (type.EndsWith("[]", StringComparison.Ordinal))
        {
            return $"{type[..^2]}?[]";
        }

        return $"{type}?";
    }

    private static bool IsNonNullableValueType(string type)
    {
        return type is "bool" or "byte" or "char" or "double" or "short" or "int" or "long" or
            "nint" or "sbyte" or "float" or "ushort" or "uint" or "ulong" or "nuint";
    }
}
