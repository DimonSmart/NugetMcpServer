using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetMcpServer.Services;

public sealed class ApiDefinitionFormatter
{
    public string FormatTypeDefinition(ApiTypeModel type, string packageId, string? packageVersion = null, string? targetFramework = null)
    {
        var sb = new StringBuilder();
        sb.Append("/* C# TYPE FROM ");
        sb.Append(type.AssemblyName);
        sb.Append(" (Package: ");
        sb.Append(packageId);
        if (!string.IsNullOrWhiteSpace(packageVersion))
        {
            sb.Append(' ');
            sb.Append(packageVersion);
        }
        if (!string.IsNullOrWhiteSpace(targetFramework))
        {
            sb.Append(", TFM: ");
            sb.Append(targetFramework);
        }
        sb.AppendLine(") */");

        if (!string.IsNullOrWhiteSpace(type.Namespace))
        {
            sb.Append("namespace ");
            sb.Append(type.Namespace);
            sb.AppendLine(";");
            sb.AppendLine();
        }

        AppendTypeHeader(sb, type);
        sb.AppendLine();
        sb.AppendLine("{");

        if (type.Kind == ApiTypeKind.Enum)
        {
            AppendEnumBody(sb, type);
        }
        else if (type.Kind == ApiTypeKind.Delegate)
        {
            AppendDelegateBody(sb, type);
        }
        else
        {
            AppendConstructors(sb, type);
            AppendFields(sb, type);
            AppendProperties(sb, type);
            AppendEvents(sb, type);
            AppendMethods(sb, type);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendTypeHeader(StringBuilder sb, ApiTypeModel type)
    {
        sb.Append("public ");
        sb.Append(type.Kind switch
        {
            ApiTypeKind.Interface => "interface ",
            ApiTypeKind.Struct => "struct ",
            ApiTypeKind.RecordStruct => "record struct ",
            ApiTypeKind.Enum => "enum ",
            ApiTypeKind.Delegate => "delegate ",
            ApiTypeKind.StaticClass => "static class ",
            ApiTypeKind.AbstractClass => "abstract class ",
            ApiTypeKind.SealedClass => "sealed class ",
            ApiTypeKind.RecordClass => "record ",
            _ => "class "
        });
        sb.Append(ShortName(type.Name));

        if (!string.IsNullOrWhiteSpace(type.BaseType))
        {
            sb.Append(" : ");
            sb.Append(ShortType(type.BaseType));
        }

        var interfaces = type.Interfaces.Select(ShortType).ToList();
        if (interfaces.Count > 0)
        {
            sb.Append(string.IsNullOrWhiteSpace(type.BaseType) ? " : " : ", ");
            sb.Append(string.Join(", ", interfaces));
        }

        AppendGenericConstraints(sb, type.GenericParameters);
    }

    private static void AppendConstructors(StringBuilder sb, ApiTypeModel type)
    {
        foreach (var ctor in type.Constructors)
        {
            sb.Append("    public ");
            sb.Append(ShortNameWithoutGeneric(type.Name));
            sb.Append('(');
            sb.Append(FormatParameters(ctor.Parameters));
            sb.AppendLine(");");
        }
    }

    private static void AppendFields(StringBuilder sb, ApiTypeModel type)
    {
        foreach (var field in type.Fields.Where(static f => f.Name != "value__"))
        {
            sb.Append("    public ");
            if (field.IsConst)
            {
                sb.Append("const ");
            }
            else if (field.IsStatic && field.IsReadOnly)
            {
                sb.Append("static readonly ");
            }

            sb.Append(ShortType(field.Type));
            sb.Append(' ');
            sb.Append(field.Name);
            if (field.LiteralValue != null)
            {
                sb.Append(" = ");
                sb.Append(field.LiteralValue);
            }
            sb.AppendLine(";");
        }
    }

    private static void AppendProperties(StringBuilder sb, ApiTypeModel type)
    {
        foreach (var property in type.Properties)
        {
            sb.Append("    public ");
            if (property.IsStatic)
            {
                sb.Append("static ");
            }
            if (property.IsRequired)
            {
                sb.Append("required ");
            }

            sb.Append(ShortType(property.Type));
            sb.Append(' ');
            if (property.IndexParameters.Count > 0)
            {
                sb.Append("this[");
                sb.Append(FormatParameters(property.IndexParameters));
                sb.Append(']');
            }
            else
            {
                sb.Append(property.Name);
            }

            sb.Append(" { ");
            if (property.HasPublicGetter)
            {
                sb.Append("get; ");
            }
            if (property.HasPublicSetter)
            {
                sb.Append(property.IsInitOnly ? "init; " : "set; ");
            }
            sb.AppendLine("}");
        }
    }

    private static void AppendEvents(StringBuilder sb, ApiTypeModel type)
    {
        foreach (var evt in type.Events)
        {
            sb.Append("    public ");
            if (evt.IsStatic)
            {
                sb.Append("static ");
            }
            sb.Append("event ");
            sb.Append(ShortType(evt.Type));
            sb.Append(' ');
            sb.Append(evt.Name);
            sb.AppendLine(";");
        }
    }

    private static void AppendMethods(StringBuilder sb, ApiTypeModel type)
    {
        foreach (var method in type.Methods)
        {
            sb.Append("    public ");
            if (method.IsStatic)
            {
                sb.Append("static ");
            }
            else if (method.IsAbstract && type.Kind != ApiTypeKind.Interface)
            {
                sb.Append("abstract ");
            }
            else if (method.IsOverride)
            {
                sb.Append("override ");
            }
            else if (method.IsVirtual && type.Kind != ApiTypeKind.Interface)
            {
                sb.Append("virtual ");
            }

            if (method.IsObsolete)
            {
                sb.Append("[Obsolete] ");
            }

            sb.Append(ShortType(method.ReturnType));
            sb.Append(' ');
            sb.Append(method.Name);
            sb.Append('(');
            sb.Append(FormatParameters(method.Parameters));
            sb.Append(')');
            AppendGenericConstraints(sb, method.GenericParameters);
            sb.AppendLine(";");
        }
    }

    private static void AppendEnumBody(StringBuilder sb, ApiTypeModel type)
    {
        for (var i = 0; i < type.EnumValues.Count; i++)
        {
            var value = type.EnumValues[i];
            sb.Append("    ");
            sb.Append(value.Name);
            if (!string.IsNullOrWhiteSpace(value.Value))
            {
                sb.Append(" = ");
                sb.Append(value.Value);
            }
            sb.AppendLine(i == type.EnumValues.Count - 1 ? string.Empty : ",");
        }
    }

    private static void AppendDelegateBody(StringBuilder sb, ApiTypeModel type)
    {
        var invoke = type.Methods.FirstOrDefault(static method => method.Name == "Invoke");
        if (invoke == null)
        {
            return;
        }

        sb.Append("    ");
        sb.Append(ShortType(invoke.ReturnType));
        sb.Append(" Invoke(");
        sb.Append(FormatParameters(invoke.Parameters));
        sb.AppendLine(");");
    }

    private static string FormatParameters(IReadOnlyList<ApiParameterModel> parameters)
    {
        return string.Join(", ", parameters.Select(FormatParameter));
    }

    private static string FormatParameter(ApiParameterModel parameter)
    {
        var prefix = parameter.IsParams ? "params " :
            parameter.IsOut ? "out " :
            parameter.IsIn ? "in " :
            parameter.IsRef ? "ref " : string.Empty;
        var formatted = $"{prefix}{ShortType(parameter.Type)} {parameter.Name}";
        if (parameter.DefaultValue != null)
        {
            formatted += $" = {parameter.DefaultValue}";
        }
        else if (parameter.IsOptional)
        {
            formatted += " = default";
        }

        return formatted;
    }

    private static void AppendGenericConstraints(StringBuilder sb, IReadOnlyList<ApiGenericParameterModel> parameters)
    {
        foreach (var parameter in parameters)
        {
            var constraints = new List<string>();
            if (parameter.HasReferenceTypeConstraint)
            {
                constraints.Add("class");
            }
            if (parameter.HasValueTypeConstraint)
            {
                constraints.Add("struct");
            }
            constraints.AddRange(parameter.Constraints.Select(ShortType).Where(static c => c != "System.ValueType"));
            if (parameter.HasDefaultConstructorConstraint && !parameter.HasValueTypeConstraint)
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
            {
                sb.AppendLine();
                sb.Append("    where ");
                sb.Append(parameter.Name);
                sb.Append(" : ");
                sb.Append(string.Join(", ", constraints.Distinct(StringComparer.Ordinal)));
            }
        }
    }

    private static string ShortName(string name) => name.Replace("+", ".", StringComparison.Ordinal);

    private static string ShortNameWithoutGeneric(string name)
    {
        var shortName = ShortName(name);
        var index = shortName.IndexOf('<', StringComparison.Ordinal);
        return index < 0 ? shortName : shortName[..index];
    }

    private static string ShortType(string type)
    {
        return type
            .Replace("System.Void", "void", StringComparison.Ordinal)
            .Replace("System.Boolean", "bool", StringComparison.Ordinal)
            .Replace("System.Byte", "byte", StringComparison.Ordinal)
            .Replace("System.Char", "char", StringComparison.Ordinal)
            .Replace("System.Double", "double", StringComparison.Ordinal)
            .Replace("System.Int16", "short", StringComparison.Ordinal)
            .Replace("System.Int32", "int", StringComparison.Ordinal)
            .Replace("System.Int64", "long", StringComparison.Ordinal)
            .Replace("System.Object", "object", StringComparison.Ordinal)
            .Replace("System.SByte", "sbyte", StringComparison.Ordinal)
            .Replace("System.Single", "float", StringComparison.Ordinal)
            .Replace("System.String", "string", StringComparison.Ordinal)
            .Replace("System.UInt16", "ushort", StringComparison.Ordinal)
            .Replace("System.UInt32", "uint", StringComparison.Ordinal)
            .Replace("System.UInt64", "ulong", StringComparison.Ordinal);
    }
}
