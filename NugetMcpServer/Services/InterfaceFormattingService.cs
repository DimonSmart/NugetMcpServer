using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

public class InterfaceFormattingService
{
    public string FormatInterfaceDefinition(Type interfaceType, string assemblyName, string packageName)
    {
        var header = $"/* C# INTERFACE FROM {assemblyName} (Package: {packageName}) */";
            
        var sb = new StringBuilder()
            .AppendLine(header);

        sb.Append($"public interface {TypeFormattingHelpers.FormatTypeName(interfaceType)}");

        if (interfaceType.IsGenericType)
        {
            var constraints = TypeFormattingHelpers.GetGenericConstraints(interfaceType);
            if (!string.IsNullOrEmpty(constraints))
                sb.Append(constraints);
        }
        sb.AppendLine().AppendLine("{");

        var processedProperties = new HashSet<string>();
        var properties = TypeFormattingHelpers.GetRegularProperties(interfaceType);

        foreach (var prop in properties)
        {
            processedProperties.Add(prop.Name);
            sb.AppendLine($"    {TypeFormattingHelpers.FormatPropertyDefinition(prop, isInterface: true)}");
        }

        var indexers = TypeFormattingHelpers.GetIndexerProperties(interfaceType);
        foreach (var indexer in indexers)
        {
            processedProperties.Add(indexer.Name);
            sb.AppendLine($"    {TypeFormattingHelpers.FormatIndexerDefinition(indexer, isInterface: true)}");
        }

        foreach (var method in interfaceType.GetMethods())
        {
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
