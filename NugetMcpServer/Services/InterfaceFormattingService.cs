using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using NuGetMcpServer.Extensions;

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
            .AppendLine($"/* C# INTERFACE FROM {assemblyName} */");        // Format the interface declaration with generics
        sb.Append($"public interface {TypeFormattingHelpers.FormatTypeName(interfaceType)}");

        // Add generic constraints if any
        if (interfaceType.IsGenericType)
        {
            var constraints = TypeFormattingHelpers.GetGenericConstraints(interfaceType);
            if (!string.IsNullOrEmpty(constraints))
                sb.Append(constraints);
        }
        sb.AppendLine().AppendLine("{");

        // Track processed property names to avoid duplicates when looking at get/set methods
        var processedProperties = new HashSet<string>();
        var properties = TypeFormattingHelpers.GetRegularProperties(interfaceType);

        // Add properties
        foreach (var prop in properties)
        {
            processedProperties.Add(prop.Name);
            sb.AppendLine($"    {TypeFormattingHelpers.FormatPropertyDefinition(prop, isInterface: true)}");
        }

        // Add indexers (special properties)
        var indexers = TypeFormattingHelpers.GetIndexerProperties(interfaceType);
        foreach (var indexer in indexers)
        {
            processedProperties.Add(indexer.Name);
            sb.AppendLine($"    {TypeFormattingHelpers.FormatIndexerDefinition(indexer, isInterface: true)}");
        }

        // Add methods (excluding property accessors)
        foreach (var method in interfaceType.GetMethods())
        {
            // Skip property accessor methods that we've already processed
            if (TypeFormattingHelpers.IsPropertyAccessor(method, processedProperties))
                continue;

            var parameters = string.Join(", ",
                method.GetParameters().Select(p => $"{TypeFormattingHelpers.FormatTypeName(p.ParameterType)} {p.Name}"));

            sb.AppendLine($"    {TypeFormattingHelpers.FormatTypeName(method.ReturnType)} {method.Name}({parameters});");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }
}
