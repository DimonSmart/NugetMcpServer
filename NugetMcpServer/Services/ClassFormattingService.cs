using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace NuGetMcpServer.Services;

public class ClassFormattingService
{
    // Builds a string representation of a class using metadata-only reflection
    // to avoid requiring all referenced assemblies to be present.
    [RequiresAssemblyFiles()]
    public string FormatClassDefinition(Type classType, string assemblyName, string packageName, byte[]? assemblyBytes = null)
    {
        if (assemblyBytes == null)
        {
            try
            {
                if (!string.IsNullOrEmpty(classType.Assembly.Location))
                {
                    assemblyBytes = File.ReadAllBytes(classType.Assembly.Location);
                }
            }
            catch
            {
                // ignore and fall back to using the loaded type
            }
        }

        if (assemblyBytes != null)
        {
            try
            {
                var corePath = typeof(object).Assembly.Location;
                var resolver = new PathAssemblyResolver(new[] { corePath });
                using var mlc = new MetadataLoadContext(resolver, typeof(object).Assembly.GetName().Name!);
                var asm = mlc.LoadFromByteArray(assemblyBytes);
                var metaType = asm.GetType(classType.FullName ?? classType.Name);
                if (metaType != null)
                {
                    try
                    {
                        return FormatClassDefinitionInternal(metaType, assemblyName, packageName, assemblyBytes);
                    }
                    catch (FileNotFoundException)
                    {
                        // Missing referenced assembly - fall back to loaded type
                    }
                }
            }
            catch
            {
                // fall back to using the provided Type instance
            }
        }

        return FormatClassDefinitionInternal(classType, assemblyName, packageName, assemblyBytes);
    }

    private static string FormatClassDefinitionInternal(Type classType, string assemblyName, string packageName, byte[]? assemblyBytes)
    {
        var header = $"/* C# CLASS FROM {assemblyName} (Package: {packageName}) */";

        var sb = new StringBuilder()
            .AppendLine(header);

        sb.Append("public ");

        if (classType.IsAbstract && classType.IsSealed)
            sb.Append("static ");
        if (classType.IsAbstract && !classType.IsSealed)
            sb.Append("abstract ");
        if (classType.IsSealed && !classType.IsAbstract && !classType.IsValueType)
            sb.Append("sealed ");

        string typeKeyword;
        if (TypeFormattingHelpers.IsRecordType(classType))
            typeKeyword = classType.IsValueType ? "record struct" : "record";
        else
            typeKeyword = classType.IsValueType ? "struct" : "class";
        sb.Append($"{typeKeyword} {TypeFormattingHelpers.FormatTypeName(classType)}");

        var baseTypeInfo = GetBaseTypeInfo(classType);
        if (!string.IsNullOrEmpty(baseTypeInfo))
            sb.Append($" : {baseTypeInfo}");

        if (classType.IsGenericType)
        {
            var constraints = TypeFormattingHelpers.GetGenericConstraints(classType);
            if (!string.IsNullOrEmpty(constraints))
                sb.Append(constraints);
        }
        sb.AppendLine().AppendLine("{");

        var processedProperties = new HashSet<string>();

        AddConstants(sb, classType);

        AddReadonlyFields(sb, classType);

        AddProperties(sb, classType, processedProperties);

        AddIndexers(sb, classType, processedProperties);

        AddEvents(sb, classType);

        AddMethods(sb, classType, processedProperties, assemblyBytes);

        AddNestedDelegates(sb, classType);

        if (sb.Length >= Environment.NewLine.Length &&
            sb.ToString(sb.Length - Environment.NewLine.Length, Environment.NewLine.Length) == Environment.NewLine)
        {
            sb.Length -= Environment.NewLine.Length;
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AddConstants(StringBuilder sb, Type classType)
    {
        var constants = TypeFormattingHelpers.GetPublicConstants(classType);
        foreach (var constant in constants)
        {
            var value = constant.GetRawConstantValue();
            var valueString = FormatConstantValue(value, constant.FieldType);
            sb.AppendLine($"    public const {TypeFormattingHelpers.FormatTypeName(constant.FieldType)} {constant.Name} = {valueString};");
        }

        if (constants.Any())
            sb.AppendLine();
    }

    private static void AddReadonlyFields(StringBuilder sb, Type classType)
    {
        var readonlyFields = TypeFormattingHelpers.GetPublicReadonlyFields(classType);
        foreach (var field in readonlyFields)
        {
            var modifiers = field.IsStatic ? "static readonly" : "readonly";
            sb.AppendLine($"    public {modifiers} {TypeFormattingHelpers.FormatTypeName(field.FieldType)} {field.Name};");
        }

        if (readonlyFields.Any())
            sb.AppendLine();
    }
    private static void AddProperties(StringBuilder sb, Type classType, HashSet<string> processedProperties)
    {
        var properties = TypeFormattingHelpers.GetRegularProperties(classType);
        foreach (var prop in properties)
        {
            processedProperties.Add(prop.Name);
            sb.AppendLine($"    {TypeFormattingHelpers.FormatPropertyDefinition(prop, isInterface: false)}");
        }

        if (properties.Any())
            sb.AppendLine();
    }
    private static void AddIndexers(StringBuilder sb, Type classType, HashSet<string> processedProperties)
    {
        var indexers = TypeFormattingHelpers.GetIndexerProperties(classType);
        foreach (var indexer in indexers)
        {
            processedProperties.Add(indexer.Name);
            sb.AppendLine($"    {TypeFormattingHelpers.FormatIndexerDefinition(indexer, isInterface: false)}");
        }

        if (indexers.Any())
            sb.AppendLine();
    }

    private static void AddEvents(StringBuilder sb, Type classType)
    {
        var events = classType.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (var evt in events)
        {
            var modifiers = evt.AddMethod?.IsStatic == true ? "static " : "";
            sb.AppendLine($"    public {modifiers}event {TypeFormattingHelpers.FormatTypeName(evt.EventHandlerType!)} {evt.Name};");
        }

        if (events.Any())
            sb.AppendLine();
    }

    private static void AddMethods(StringBuilder sb, Type classType, HashSet<string> processedProperties, byte[]? assemblyBytes)
    {
        var methods = classType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => !TypeFormattingHelpers.IsPropertyAccessor(m, processedProperties) &&
                       !TypeFormattingHelpers.IsEventAccessor(m) &&
                       !m.IsSpecialName &&
                       m.DeclaringType == classType);

        foreach (var method in methods)
        {
            var signature = FormatMethod(method, assemblyBytes);
            sb.AppendLine($"    {signature}");
        }

        if (methods.Any())
            sb.AppendLine();
    }

    [RequiresAssemblyFiles("Calls System.Reflection.Module.FullyQualifiedName")]
    private static string FormatMethod(MethodInfo method, byte[]? assemblyBytes)
    {
        assemblyBytes ??= TryReadAssemblyBytes(method.Module.FullyQualifiedName);
        if (assemblyBytes == null || !TryFormatMethodFromMetadata(method, assemblyBytes, out var signature))
            throw new InvalidOperationException($"Unable to format method {method.Name} using metadata");

        return signature;
    }

    private static byte[]? TryReadAssemblyBytes(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return File.ReadAllBytes(path);
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static bool TryFormatMethodFromMetadata(MethodInfo method, byte[] assemblyBytes, out string signature)
    {
        signature = string.Empty;

        try
        {
            using var ms = new MemoryStream(assemblyBytes);
            using var peReader = new PEReader(ms);
            var reader = peReader.GetMetadataReader();

            var handle = MetadataTokens.MethodDefinitionHandle(method.MetadataToken);
            var methodDef = reader.GetMethodDefinition(handle);

            var provider = new MetadataTypeNameProvider(reader);
            object? context = null;
            if (method.DeclaringType?.IsGenericType == true && !method.DeclaringType.IsGenericTypeDefinition)
            {
                context = method.DeclaringType.GetGenericArguments();
            }
            var sig = methodDef.DecodeSignature(provider, context);

            var returnType = sig.ReturnType;

            var paramHandles = methodDef.GetParameters();
            var paramTypes = sig.ParameterTypes.ToArray();
            var paramNames = new List<string>();
            foreach (var ph in paramHandles)
            {
                var param = reader.GetParameter(ph);
                if (param.SequenceNumber == 0)
                    continue; // skip return
                paramNames.Add(reader.GetString(param.Name));
            }

            var parameters = new List<string>();
            for (int i = 0; i < paramNames.Count && i < paramTypes.Length; i++)
            {
                parameters.Add($"{paramTypes[i]} {paramNames[i]}");
            }

            var modifiers = GetMethodModifiersFromAttributes(methodDef.Attributes);
            signature = $"public {modifiers}{returnType} {reader.GetString(methodDef.Name)}({string.Join(", ", parameters)});";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetMethodModifiersFromAttributes(MethodAttributes attributes)
    {
        if (attributes.HasFlag(MethodAttributes.Static))
            return "static ";

        if (attributes.HasFlag(MethodAttributes.Virtual) && !attributes.HasFlag(MethodAttributes.Abstract))
            return "virtual ";

        if (attributes.HasFlag(MethodAttributes.Abstract))
            return "abstract ";

        return string.Empty;
    }

    private sealed class MetadataTypeNameProvider : ISignatureTypeProvider<string, object?>
    {
        private readonly MetadataReader _reader;

        public MetadataTypeNameProvider(MetadataReader reader)
        {
            _reader = reader;
        }

        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "nint";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(", ", typeArguments)}>";
        public string GetGenericMethodParameter(object? genericContext, int index) => $"T{index}";
        public string GetGenericTypeParameter(object? genericContext, int index)
        {
            if (genericContext is Type[] args && index < args.Length)
                return TypeFormattingHelpers.FormatTypeName(args[index]);
            return $"T{index}";
        }
        public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetPinnedType(string elementType) => elementType;
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
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
            PrimitiveTypeCode.UInt16 => "ushort",
            PrimitiveTypeCode.UInt32 => "uint",
            PrimitiveTypeCode.UInt64 => "ulong",
            PrimitiveTypeCode.UIntPtr => "nuint",
            PrimitiveTypeCode.Void => "void",
            _ => typeCode.ToString().ToLowerInvariant()
        };
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var def = reader.GetTypeDefinition(handle);
            var name = reader.GetString(def.Name);
            var ns = reader.GetString(def.Namespace);
            return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        }
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var tr = reader.GetTypeReference(handle);
            var name = reader.GetString(tr.Name);
            var ns = reader.GetString(tr.Namespace);
            return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        }
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            var ts = reader.GetTypeSpecification(handle);
            return ts.DecodeSignature(this, genericContext);
        }
    }

    private static void AddNestedDelegates(StringBuilder sb, Type classType)
    {
        var nestedTypes = classType.GetNestedTypes(BindingFlags.Public)
            .Where(t => typeof(Delegate).IsAssignableFrom(t));

        foreach (var delegateType in nestedTypes)
        {
            var invokeMethod = delegateType.GetMethod("Invoke");
            if (invokeMethod != null)
            {
                var parameters = string.Join(", ",
                    invokeMethod.GetParameters().Select(p => $"{TypeFormattingHelpers.FormatTypeName(p.ParameterType)} {p.Name}"));

                sb.AppendLine($"    public delegate {TypeFormattingHelpers.FormatTypeName(invokeMethod.ReturnType)} {delegateType.Name}({parameters});");
            }
        }
    }
    private static string GetBaseTypeInfo(Type classType)
    {
        var parts = new List<string>();

        if (classType.BaseType != null && classType.BaseType != typeof(object))
        {
            parts.Add(TypeFormattingHelpers.FormatTypeName(classType.BaseType));
        }

        var interfaces = classType.GetInterfaces()
            .Where(i => !classType.BaseType?.GetInterfaces().Contains(i) == true);

        foreach (var iface in interfaces)
        {
            parts.Add(TypeFormattingHelpers.FormatTypeName(iface));
        }

        return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
    }

    private static string FormatConstantValue(object? value, Type fieldType)
    {
        return value switch
        {
            null => "null",
            string str => $"\"{str}\"",
            char ch => $"'{ch}'",
            bool b => b ? "true" : "false",
            float f => $"{f}f",
            double d => $"{d}d",
            decimal m => $"{m}m",
            long l => $"{l}L",
            ulong ul => $"{ul}UL",
            uint ui => $"{ui}U",
            _ => value.ToString() ?? "null"
        };
    }

}
