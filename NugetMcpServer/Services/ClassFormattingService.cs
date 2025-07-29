using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;

namespace NuGetMcpServer.Services;

public class ClassFormattingService
{
    // Builds a string representation of a class, including its properties,
    // methods, constants, delegates, and other public members
    public string FormatClassDefinition(Type classType, string assemblyName, string packageName, byte[]? assemblyBytes = null)
    {
        try
        {
            return FormatClassDefinitionReflection(classType, assemblyName, packageName);
        }
        catch (Exception) when (assemblyBytes != null)
        {
            return FormatClassDefinitionMetadata(assemblyBytes, classType.FullName ?? classType.Name, assemblyName, packageName);
        }
    }

    private static string FormatClassDefinitionReflection(Type classType, string assemblyName, string packageName)
    {
        var header = $"/* C# CLASS FROM {assemblyName} (Package: {packageName}) */";

        var sb = new StringBuilder()
            .AppendLine(header);

        sb.Append("public ");

        if (classType.IsAbstract && classType.IsSealed)
            sb.Append("static ");
        else if (classType.IsAbstract)
            sb.Append("abstract ");
        else if (classType.IsSealed && !classType.IsValueType)
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

        AddMethods(sb, classType, processedProperties);

        AddNestedDelegates(sb, classType);

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

    private static void AddMethods(StringBuilder sb, Type classType, HashSet<string> processedProperties)
    {
        var methods = classType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => !TypeFormattingHelpers.IsPropertyAccessor(m, processedProperties) &&
                       !TypeFormattingHelpers.IsEventAccessor(m) &&
                       !m.IsSpecialName &&
                       m.DeclaringType == classType);

        foreach (var method in methods)
        {
            var parameters = string.Join(", ",
                method.GetParameters().Select(p => $"{TypeFormattingHelpers.FormatTypeName(p.ParameterType)} {p.Name}"));

            var modifiers = TypeFormattingHelpers.GetMethodModifiers(method);
            sb.AppendLine($"    public {modifiers}{TypeFormattingHelpers.FormatTypeName(method.ReturnType)} {method.Name}({parameters});");
        }

        if (methods.Any())
            sb.AppendLine();
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

    private static string FormatClassDefinitionMetadata(byte[] assemblyBytes, string fullName, string assemblyName, string packageName)
    {
        var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(new System.IO.MemoryStream(assemblyBytes));
        var typeDef = asm.MainModule.GetType(fullName.Replace('+', '/')) ?? asm.MainModule.Types.FirstOrDefault(t => t.FullName == fullName.Replace('+', '/'));
        if (typeDef == null)
            return $"/* C# CLASS FROM {assemblyName} (Package: {packageName}) */\n// Type '{fullName}' not found";

        var sb = new StringBuilder().AppendLine($"/* C# CLASS FROM {assemblyName} (Package: {packageName}) */");

        sb.Append("public ");
        if (typeDef.IsAbstract && typeDef.IsSealed)
            sb.Append("static ");
        else if (typeDef.IsAbstract && !typeDef.IsInterface)
            sb.Append("abstract ");
        else if (typeDef.IsSealed && !typeDef.IsValueType)
            sb.Append("sealed ");

        string typeKeyword = IsRecordType(typeDef) ? (typeDef.IsValueType ? "record struct" : "record") : (typeDef.IsValueType ? "struct" : "class");
        sb.Append($"{typeKeyword} {FormatTypeName(typeDef)}");

        var baseTypes = new List<string>();
        if (typeDef.BaseType != null && typeDef.BaseType.FullName != "System.Object")
            baseTypes.Add(FormatTypeName(typeDef.BaseType));
        foreach (var iface in typeDef.Interfaces.Select(i => i.InterfaceType))
            baseTypes.Add(FormatTypeName(iface));
        if (baseTypes.Count > 0)
            sb.Append($" : {string.Join(", ", baseTypes)}");

        sb.AppendLine().AppendLine("{");

        foreach (var field in typeDef.Fields.Where(f => f.IsPublic && f.HasConstant))
        {
            sb.AppendLine($"    public const {FormatTypeName(field.FieldType)} {field.Name} = {field.Constant};");
        }

        foreach (var field in typeDef.Fields.Where(f => f.IsPublic && f.IsInitOnly && !f.IsLiteral))
        {
            var modifier = field.IsStatic ? "static readonly" : "readonly";
            sb.AppendLine($"    public {modifier} {FormatTypeName(field.FieldType)} {field.Name};");
        }

        foreach (var prop in typeDef.Properties.Where(p => p.GetMethod?.IsPublic == true || p.SetMethod?.IsPublic == true))
        {
            sb.Append($"    public {FormatTypeName(prop.PropertyType)} {prop.Name} {{ ");
            if (prop.GetMethod?.IsPublic == true) sb.Append("get; ");
            if (prop.SetMethod?.IsPublic == true) sb.Append("set; ");
            sb.AppendLine("}");
        }

        foreach (var method in typeDef.Methods.Where(m => m.IsPublic && !m.IsSpecialName))
        {
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));
            sb.AppendLine($"    public {FormatTypeName(method.ReturnType)} {method.Name}({parameters});");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsRecordType(Mono.Cecil.TypeDefinition typeDef)
    {
        var printMembers = typeDef.Methods.FirstOrDefault(m => m.Name == "PrintMembers" && !m.IsPublic && !m.IsStatic);
        if (printMembers == null)
            return false;
        return typeDef.Properties.Any(p => p.Name == "EqualityContract") || typeDef.IsValueType;
    }

    private static string FormatTypeName(Mono.Cecil.TypeReference type)
    {
        if (type is Mono.Cecil.GenericInstanceType git)
        {
            var name = git.ElementType.Name.Split('`')[0];
            var args = string.Join(", ", git.GenericArguments.Select(FormatTypeName));
            return $"{name}<{args}>";
        }

        if (type.IsGenericParameter)
            return type.Name;

        var primitive = type.FullName switch
        {
            "System.Int32" => "int",
            "System.String" => "string",
            "System.Boolean" => "bool",
            "System.Double" => "double",
            "System.Single" => "float",
            "System.Int64" => "long",
            "System.Int16" => "short",
            "System.Byte" => "byte",
            "System.Char" => "char",
            "System.Object" => "object",
            "System.Decimal" => "decimal",
            "System.Void" => "void",
            "System.UInt64" => "ulong",
            "System.UInt32" => "uint",
            "System.UInt16" => "ushort",
            "System.SByte" => "sbyte",
            _ => null
        };
        if (primitive != null)
            return primitive;

        return type.Name.Split('`')[0];
    }
}
