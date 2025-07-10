using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NuGetMcpServer.Services;

public class ClassFormattingService
{
    // Builds a string representation of a class, including its properties, 
    // methods, constants, delegates, and other public members
    public string FormatClassDefinition(Type classType, string assemblyName, string? packageName = null)
    {
        var header = packageName != null 
            ? $"/* C# CLASS FROM {assemblyName} (Package: {packageName}) */"
            : $"/* C# CLASS FROM {assemblyName} */";
            
        var sb = new StringBuilder()
            .AppendLine(header);

        sb.Append("public ");

        if (classType.IsAbstract && classType.IsSealed)
            sb.Append("static ");
        else if (classType.IsAbstract)
            sb.Append("abstract ");
        else if (classType.IsSealed && !classType.IsValueType)
            sb.Append("sealed ");

        string typeKeyword = classType.IsValueType ? "struct" : "class";
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
}
