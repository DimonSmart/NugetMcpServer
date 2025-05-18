using NuGetMcpServer.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NuGetMcpServer.Services;

/// <summary>
/// Service for formatting interface definitions
/// </summary>
public class InterfaceFormattingService
{
    /// <summary>
    /// Builds a string representation of an interface, including its properties, 
    /// indexers, methods, and generic constraints
    /// </summary>
    public string FormatInterfaceDefinition(Type interfaceType, string assemblyName)
    {
        var sb = new StringBuilder()
            .AppendLine($"/* C# INTERFACE FROM {assemblyName} */");

        // Format the interface declaration with generics
        sb.Append($"public interface {FormatTypeName(interfaceType)}");

        // Add generic constraints if any
        if (interfaceType.IsGenericType)
        {
            var constraints = GetGenericConstraints(interfaceType);
            if (!string.IsNullOrEmpty(constraints))
                sb.Append(constraints);
        }

        sb.AppendLine().AppendLine("{");

        // Track processed property names to avoid duplicates when looking at get/set methods
        var processedProperties = new HashSet<string>();
        var properties = GetInterfaceProperties(interfaceType);

        // Add properties
        foreach (var prop in properties)
        {
            processedProperties.Add(prop.Name);

            sb.Append($"    {FormatTypeName(prop.PropertyType)} {prop.Name} {{ ");

            if (prop.GetGetMethod() != null)
                sb.Append("get; ");

            if (prop.GetSetMethod() != null)
                sb.Append("set; ");

            sb.AppendLine("}");
        }

        // Add indexers (special properties)
        var indexers = GetInterfaceIndexers(interfaceType);
        foreach (var indexer in indexers)
        {
            processedProperties.Add(indexer.Name);

            var parameters = indexer.GetIndexParameters();
            var paramList = string.Join(", ", parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

            sb.Append($"    {FormatTypeName(indexer.PropertyType)} this[{paramList}] {{ ");

            if (indexer.GetGetMethod() != null)
                sb.Append("get; ");

            if (indexer.GetSetMethod() != null)
                sb.Append("set; ");

            sb.AppendLine("}");
        }

        // Add methods (excluding property accessors)
        foreach (var method in interfaceType.GetMethods())
        {
            // Skip property accessor methods that we've already processed
            if (IsPropertyAccessor(method, processedProperties))
                continue;

            var parameters = string.Join(", ",
                method.GetParameters().Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

            sb.AppendLine($"    {FormatTypeName(method.ReturnType)} {method.Name}({parameters});");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsPropertyAccessor(MethodInfo method, HashSet<string> processedProperties)
    {
        if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
        {
            var propertyName = method.Name.Substring(4); // Skip get_ or set_
            return processedProperties.Contains(propertyName);
        }
        return false;
    }

    private static string FormatTypeName(Type type) => type.FormatCSharpTypeName();    /// <summary>
                                                                                       /// Builds the 'where T : [constraints]' string for generic interface parameters
                                                                                       /// </summary>
    private string GetGenericConstraints(Type interfaceType)
    {
        if (!interfaceType.IsGenericType)
            return string.Empty;

        var constraints = new StringBuilder();
        var genericArgs = interfaceType.GetGenericArguments();

        foreach (var arg in genericArgs)
        {
            // We can only check GenericParameterAttributes for generic parameters,
            // not for concrete type arguments like in IMockGeneric<string>
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
    }

    private static IEnumerable<PropertyInfo> GetInterfaceProperties(Type interfaceType)
    {
        var properties = interfaceType.GetProperties();
        return properties.Where(p => p.GetIndexParameters().Length == 0);
    }

    private static IEnumerable<PropertyInfo> GetInterfaceIndexers(Type interfaceType)
    {
        var properties = interfaceType.GetProperties();
        return properties.Where(p => p.GetIndexParameters().Length > 0);
    }
}
