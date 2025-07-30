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
        PropertyInfo? equalityContract = type.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo? printMembers = type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance);
        if (printMembers == null)
            return false;

        return equalityContract != null || type.IsValueType;
    }

    // Builds the 'where T : [constraints]' string for generic type parameters
    public static string GetGenericConstraints(Type type)
    {
        if (!type.IsGenericType)
            return string.Empty;

        StringBuilder constraints = new StringBuilder();
        Type[] genericArgs = type.GetGenericArguments();

        foreach (Type arg in genericArgs)
        {
            // We can only check GenericParameterAttributes for generic parameters,
            // not for concrete type arguments like in IMyClass<string>
            if (arg.IsGenericParameter)
            {
                List<string> argConstraints = new List<string>();

                // Reference type constraint
                if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
                    argConstraints.Add("class");

                // Value type constraint
                if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                    argConstraints.Add("struct");

                // Constructor constraint
                if (arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) &&
                    !arg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                {
                    argConstraints.Add("new()");
                }

                // Interface constraints
                foreach (Type constraint in arg.GetGenericParameterConstraints())
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
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (PropertyInfo property in properties)
        {
            bool isIndexer;
            try
            {
                isIndexer = property.GetIndexParameters().Length > 0;
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is TypeLoadException)
            {
                // Skip property if index parameters can't be resolved
                continue;
            }

            if (!isIndexer)
                yield return property;
        }
    }

    // Gets all indexer properties
    public static IEnumerable<PropertyInfo> GetIndexerProperties(Type type)
    {
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        foreach (PropertyInfo property in properties)
        {
            bool isIndexer;
            try
            {
                isIndexer = property.GetIndexParameters().Length > 0;
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is TypeLoadException)
            {
                // Skip property if index parameters can't be resolved
                continue;
            }

            if (isIndexer)
                yield return property;
        }
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
            string propertyName = method.Name.Substring(4); // Skip get_ or set_
            return processedProperties.Contains(propertyName);
        }
        return false;
    }

    // Checks if a method is an event accessor method (add_/remove_)
    public static bool IsEventAccessor(MethodInfo method)
    {
        return method.Name.StartsWith("add_") || method.Name.StartsWith("remove_");
    }

    /// <summary>
    /// Formats property modifiers (static, virtual, abstract, etc.)
    /// </summary>
    public static string GetPropertyModifiers(PropertyInfo property)
    {
        List<string> modifiers = new List<string>();

        MethodInfo? getter = property.GetGetMethod();
        MethodInfo? setter = property.GetSetMethod();

        if (getter?.IsStatic == true || setter?.IsStatic == true)
            modifiers.Add("static");
        else if (getter?.IsVirtual == true && getter?.IsAbstract != true) modifiers.Add("virtual");
        else if (getter?.IsAbstract == true)
            modifiers.Add("abstract");

        return modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
    }

    public static string FormatPropertyDefinition(PropertyInfo property, bool isInterface = false)
    {
        StringBuilder sb = new StringBuilder();

        if (!isInterface)
        {
            sb.Append("public ");
            string modifiers = GetPropertyModifiers(property);
            sb.Append(modifiers);
        }

        sb.Append($"{FormatTypeName(property.PropertyType)} {property.Name} {{ ");

        if (property.GetGetMethod() != null)
            sb.Append("get; ");

        if (property.GetSetMethod() != null)
            sb.Append("set; ");

        sb.Append("}");
        return sb.ToString();
    }

    // Formats an indexer definition for both interfaces and classes
    public static string FormatIndexerDefinition(PropertyInfo indexer, bool isInterface = false)
    {
        ParameterInfo[] parameters = indexer.GetIndexParameters();
        string paramList = string.Join(", ", parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

        var sb = new StringBuilder();

        if (!isInterface)
        {
            sb.Append("public ");
            string modifiers = GetPropertyModifiers(indexer);
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
