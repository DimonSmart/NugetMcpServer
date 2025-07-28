using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using NuGetMcpServer.Extensions;

namespace NuGetMcpServer.Services;

public static class TypeFormattingHelpers
{
    public static string FormatTypeName(Type type) => type.FormatCSharpTypeName();

    public static bool IsRecordType(Type type)
    {
        var equalityContract = type.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance);
        var printMembers = type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance);
        if (printMembers == null)
            return false;

        return equalityContract != null || type.IsValueType;
    }

    // Builds the 'where T : [constraints]' string for generic type parameters
    public static string GetGenericConstraints(Type type)
    {
        if (!type.IsGenericType)
            return string.Empty;

        var constraints = new StringBuilder();
        var genericArgs = type.GetGenericArguments();

        foreach (var arg in genericArgs)
        {
            // We can only check GenericParameterAttributes for generic parameters,
            // not for concrete type arguments like in IMyClass<string>
            if (arg.IsGenericParameter)
            {
                var argConstraints = new List<string>();

                // Reference type constraint
                if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
                    argConstraints.Add("class");

                // Value type constraint
                if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                    argConstraints.Add("struct");

                // Constructor constraint
                if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) &&
                    !arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                    argConstraints.Add("new()");

                // Interface constraints
                foreach (var constraint in arg.GetGenericParameterConstraints())
                {
                    if (constraint != typeof(ValueType)) // Skip ValueType for struct constraint
                        argConstraints.Add(FormatTypeName(constraint));
                }

                if (argConstraints.Count > 0)
                    constraints.AppendLine($" where {arg.Name} : {string.Join(", ", argConstraints)}");
            }
        }

        return constraints.ToString();
    }    // Gets all properties that are not indexers
    public static IEnumerable<PropertyInfo> GetRegularProperties(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        return properties.Where(p => p.GetIndexParameters().Length == 0);
    }

    // Gets all indexer properties
    public static IEnumerable<PropertyInfo> GetIndexerProperties(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        return properties.Where(p => p.GetIndexParameters().Length > 0);
    }

    // Gets all public fields that are constants
    public static IEnumerable<FieldInfo> GetPublicConstants(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly);
    }

    // Gets all public fields that are readonly static
    public static IEnumerable<FieldInfo> GetPublicReadonlyFields(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsInitOnly);
    }

    // Checks if a method is a property accessor method (get_/set_)
    public static bool IsPropertyAccessor(MethodInfo method, HashSet<string> processedProperties)
    {
        if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
        {
            var propertyName = method.Name.Substring(4); // Skip get_ or set_
            return processedProperties.Contains(propertyName);
        }
        return false;
    }

    // Checks if a method is an event accessor method (add_/remove_)
    public static bool IsEventAccessor(MethodInfo method)
    {
        return method.Name.StartsWith("add_") || method.Name.StartsWith("remove_");
    }

    public static string GetMethodModifiers(MethodInfo method)
    {
        var modifiers = new List<string>();

        if (method.IsStatic)
            modifiers.Add("static");
        else if (method.IsVirtual && !method.IsAbstract)
            modifiers.Add("virtual");
        else if (method.IsAbstract)
            modifiers.Add("abstract");

        return modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
    }

    /// <summary>
    /// Formats property modifiers (static, virtual, abstract, etc.)
    /// </summary>
    public static string GetPropertyModifiers(PropertyInfo property)
    {
        var modifiers = new List<string>();

        var getter = property.GetGetMethod();
        var setter = property.GetSetMethod();

        if (getter?.IsStatic == true || setter?.IsStatic == true)
            modifiers.Add("static");
        else if (getter?.IsVirtual == true && getter?.IsAbstract != true) modifiers.Add("virtual");
        else if (getter?.IsAbstract == true)
            modifiers.Add("abstract");

        return modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
    }

    public static string FormatPropertyDefinition(PropertyInfo property, bool isInterface = false)
    {
        var sb = new StringBuilder();

        if (!isInterface)
        {
            sb.Append("public ");
            var modifiers = GetPropertyModifiers(property);
            sb.Append(modifiers);
        }

        sb.Append($"{FormatTypeName(property.PropertyType)} {property.Name} {{ ");

        if (property.GetGetMethod() != null)
            sb.Append("get; ");

        if (property.GetSetMethod() != null)
            sb.Append("set; ");

        sb.Append("}");
        return sb.ToString();
    }    // Formats an indexer definition for both interfaces and classes
    public static string FormatIndexerDefinition(PropertyInfo indexer, bool isInterface = false)
    {
        var parameters = indexer.GetIndexParameters();
        var paramList = string.Join(", ", parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

        var sb = new StringBuilder();

        if (!isInterface)
        {
            sb.Append("public ");
            var modifiers = GetPropertyModifiers(indexer);
            sb.Append(modifiers);
        }

        sb.Append($"{FormatTypeName(indexer.PropertyType)} this[{paramList}] {{ ");

        if (indexer.GetGetMethod() != null)
            sb.Append("get; ");

        if (indexer.GetSetMethod() != null)
            sb.Append("set; ");

        sb.Append("}");
        return sb.ToString();
    }
}
