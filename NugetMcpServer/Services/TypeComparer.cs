using System;
using System.Collections.Generic;
using System.Linq;
using NuGetMcpServer.Models;

namespace NuGetMcpServer.Services;

public class TypeComparer
{
    public List<TypeChange> CompareTypes(ApiTypeModel oldType, ApiTypeModel newType)
    {
        var changes = new List<TypeChange>();

        if (!StringEquals(oldType.BaseType, newType.BaseType))
        {
            changes.Add(CreateChange(ChangeCategory.BaseClassChanged, ChangeSeverity.High, newType,
                from: oldType.BaseType, to: newType.BaseType,
                impact: "Base class change may break inheritance-based code"));
        }

        changes.AddRange(CompareSets(oldType, newType, oldType.Interfaces, newType.Interfaces,
            ChangeCategory.InterfaceRemoved, ChangeCategory.InterfaceAdded,
            removedImpact: "Interface implementation removed - code expecting this interface may break",
            addedImpact: "Interface implementation added - non-breaking change"));

        if (!oldType.IsSealed && newType.IsSealed)
        {
            changes.Add(CreateChange(ChangeCategory.SealedAdded, ChangeSeverity.High, newType,
                impact: "Type is now sealed - inheritance no longer possible"));
        }

        if (!oldType.IsAbstract && newType.IsAbstract)
        {
            changes.Add(CreateChange(ChangeCategory.AbstractAdded, ChangeSeverity.High, newType,
                impact: "Type is now abstract - direct instantiation no longer possible"));
        }

        CompareGenericParameters(oldType, newType, changes);

        if (oldType.Kind == ApiTypeKind.Enum && newType.Kind == ApiTypeKind.Enum)
        {
            CompareEnumValues(oldType, newType, changes);
            return changes;
        }

        CompareMembers(oldType, newType, changes);
        return changes;
    }

    public TypeChange CreateTypeRemovedChange(ApiTypeModel type)
    {
        return CreateChange(ChangeCategory.TypeRemoved, ChangeSeverity.High, type,
            impact: "Type removed - all code using this type will break");
    }

    public TypeChange CreateTypeAddedChange(ApiTypeModel type)
    {
        return CreateChange(ChangeCategory.TypeAdded, ChangeSeverity.Low, type,
            impact: "New type added - non-breaking change");
    }

    private static void CompareMembers(ApiTypeModel oldType, ApiTypeModel newType, List<TypeChange> changes)
    {
        CompareMembersByIdentity(
            oldType,
            newType,
            oldType.Methods.ToDictionary(static method => method.Identity, StringComparer.Ordinal),
            newType.Methods.ToDictionary(static method => method.Identity, StringComparer.Ordinal),
            static method => method.Name,
            static method => $"{method.ReturnType} {method.Name}({string.Join(", ", method.Parameters.Select(static p => p.Type))})",
            changes);

        CompareMembersByIdentity(
            oldType,
            newType,
            oldType.Properties.ToDictionary(static property => property.Identity, StringComparer.Ordinal),
            newType.Properties.ToDictionary(static property => property.Identity, StringComparer.Ordinal),
            static property => property.Name,
            static property => $"{property.Type} {property.Name}",
            changes);

        CompareMembersByIdentity(
            oldType,
            newType,
            oldType.Fields.ToDictionary(static field => $"F:{field.Name}", StringComparer.Ordinal),
            newType.Fields.ToDictionary(static field => $"F:{field.Name}", StringComparer.Ordinal),
            static field => field.Name,
            static field => $"{field.Type} {field.Name}",
            changes);

        CompareMembersByIdentity(
            oldType,
            newType,
            oldType.Events.ToDictionary(static evt => $"E:{evt.Name}", StringComparer.Ordinal),
            newType.Events.ToDictionary(static evt => $"E:{evt.Name}", StringComparer.Ordinal),
            static evt => evt.Name,
            static evt => $"{evt.Type} {evt.Name}",
            changes);

        foreach (var oldProperty in oldType.Properties)
        {
            var newProperty = newType.Properties.FirstOrDefault(p => p.Identity == oldProperty.Identity);
            if (newProperty != null && !StringEquals(oldProperty.Type, newProperty.Type))
            {
                changes.Add(CreateMemberChange(ChangeCategory.MemberTypeChanged, ChangeSeverity.High, newType,
                    oldProperty.Name, oldProperty.Type, newProperty.Type,
                    $"Property type changed from {oldProperty.Type} to {newProperty.Type}"));
            }
        }

        foreach (var oldField in oldType.Fields)
        {
            var newField = newType.Fields.FirstOrDefault(f => f.Name == oldField.Name);
            if (newField != null && !StringEquals(oldField.Type, newField.Type))
            {
                changes.Add(CreateMemberChange(ChangeCategory.MemberTypeChanged, ChangeSeverity.High, newType,
                    oldField.Name, oldField.Type, newField.Type,
                    $"Field type changed from {oldField.Type} to {newField.Type}"));
            }
        }

        foreach (var oldMethod in oldType.Methods)
        {
            var newMethod = newType.Methods.FirstOrDefault(m => m.Identity == oldMethod.Identity);
            if (newMethod == null)
            {
                continue;
            }

            if (!StringEquals(oldMethod.ReturnType, newMethod.ReturnType))
            {
                changes.Add(CreateMemberChange(ChangeCategory.ReturnTypeChanged, ChangeSeverity.High, newType,
                    oldMethod.Name, oldMethod.ReturnType, newMethod.ReturnType,
                    "Return type changed - may break code expecting original type"));
            }

            if (oldMethod.IsVirtual && !newMethod.IsVirtual)
            {
                changes.Add(CreateMemberChange(ChangeCategory.VirtualRemoved, ChangeSeverity.High, newType,
                    oldMethod.Name, impact: "Method no longer virtual - overrides in derived classes will break"));
            }

            if (!oldMethod.IsObsolete && newMethod.IsObsolete)
            {
                changes.Add(CreateMemberChange(ChangeCategory.MemberObsoleted, ChangeSeverity.Medium, newType,
                    oldMethod.Name, impact: "Member marked as obsolete - should be replaced"));
            }
        }
    }

    private static void CompareMembersByIdentity<T>(
        ApiTypeModel oldType,
        ApiTypeModel newType,
        Dictionary<string, T> oldMembers,
        Dictionary<string, T> newMembers,
        Func<T, string> nameSelector,
        Func<T, string> signatureSelector,
        List<TypeChange> changes)
    {
        foreach (var (identity, member) in oldMembers)
        {
            if (!newMembers.ContainsKey(identity))
            {
                changes.Add(CreateMemberChange(ChangeCategory.MemberRemoved, ChangeSeverity.High, newType,
                    nameSelector(member), from: signatureSelector(member),
                    impact: $"Member {nameSelector(member)} removed - code using it will break"));
            }
        }

        foreach (var (identity, member) in newMembers)
        {
            if (!oldMembers.ContainsKey(identity))
            {
                changes.Add(CreateMemberChange(ChangeCategory.MemberAdded, ChangeSeverity.Low, newType,
                    nameSelector(member), to: signatureSelector(member),
                    impact: "New member added - non-breaking change"));
            }
        }
    }

    private static void CompareGenericParameters(ApiTypeModel oldType, ApiTypeModel newType, List<TypeChange> changes)
    {
        if (oldType.GenericParameters.Count != newType.GenericParameters.Count)
        {
            changes.Add(CreateChange(ChangeCategory.GenericParametersChanged, ChangeSeverity.High, newType,
                from: $"{oldType.GenericParameters.Count} generic parameters",
                to: $"{newType.GenericParameters.Count} generic parameters",
                impact: "Generic parameter count changed - breaks type references"));
            return;
        }

        for (var i = 0; i < oldType.GenericParameters.Count; i++)
        {
            var oldConstraints = ConstraintIdentity(oldType.GenericParameters[i]);
            var newConstraints = ConstraintIdentity(newType.GenericParameters[i]);
            if (!StringEquals(oldConstraints, newConstraints))
            {
                changes.Add(CreateChange(ChangeCategory.GenericParametersChanged, ChangeSeverity.High, newType,
                    from: oldConstraints, to: newConstraints,
                    impact: "Generic parameter constraints changed"));
            }
        }
    }

    private static void CompareEnumValues(ApiTypeModel oldType, ApiTypeModel newType, List<TypeChange> changes)
    {
        var oldValues = oldType.EnumValues.ToDictionary(static value => value.Name, StringComparer.Ordinal);
        var newValues = newType.EnumValues.ToDictionary(static value => value.Name, StringComparer.Ordinal);

        foreach (var (name, value) in oldValues)
        {
            if (!newValues.ContainsKey(name))
            {
                changes.Add(CreateMemberChange(ChangeCategory.EnumValueRemoved, ChangeSeverity.High, newType,
                    name, from: value.Value,
                    impact: $"Enum value {name} removed - code using this value will break"));
            }
        }

        foreach (var (name, value) in newValues)
        {
            if (!oldValues.ContainsKey(name))
            {
                changes.Add(CreateMemberChange(ChangeCategory.EnumValueAdded, ChangeSeverity.Low, newType,
                    name, to: value.Value,
                    impact: "New enum value added - may require handling in switch statements"));
            }
            else if (!StringEquals(oldValues[name].Value, value.Value))
            {
                changes.Add(CreateMemberChange(ChangeCategory.MemberTypeChanged, ChangeSeverity.High, newType,
                    name, oldValues[name].Value, value.Value,
                    "Enum literal value changed"));
            }
        }
    }

    private static IEnumerable<TypeChange> CompareSets(
        ApiTypeModel oldType,
        ApiTypeModel newType,
        IReadOnlyList<string> oldValues,
        IReadOnlyList<string> newValues,
        ChangeCategory removedCategory,
        ChangeCategory addedCategory,
        string removedImpact,
        string addedImpact)
    {
        var oldSet = oldValues.ToHashSet(StringComparer.Ordinal);
        var newSet = newValues.ToHashSet(StringComparer.Ordinal);

        foreach (var removed in oldSet.Except(newSet))
        {
            yield return CreateChange(removedCategory, ChangeSeverity.High, newType, from: removed, impact: removedImpact);
        }

        foreach (var added in newSet.Except(oldSet))
        {
            yield return CreateChange(addedCategory, ChangeSeverity.Low, newType, to: added, impact: addedImpact);
        }
    }

    private static string ConstraintIdentity(ApiGenericParameterModel parameter)
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
        constraints.AddRange(parameter.Constraints);
        if (parameter.HasDefaultConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return string.Join(",", constraints.OrderBy(static value => value, StringComparer.Ordinal));
    }

    private static TypeChange CreateChange(
        ChangeCategory category,
        ChangeSeverity severity,
        ApiTypeModel type,
        string? from = null,
        string? to = null,
        string? impact = null)
    {
        return new TypeChange
        {
            Category = category,
            Severity = severity,
            TypeName = type.Name,
            TypeFullName = type.FullName,
            From = from,
            To = to,
            Impact = impact
        };
    }

    private static TypeChange CreateMemberChange(
        ChangeCategory category,
        ChangeSeverity severity,
        ApiTypeModel type,
        string memberName,
        string? from = null,
        string? to = null,
        string? impact = null)
    {
        return new TypeChange
        {
            Category = category,
            Severity = severity,
            TypeName = type.Name,
            TypeFullName = type.FullName,
            MemberName = memberName,
            From = from,
            To = to,
            FromType = from,
            ToType = to,
            Impact = impact
        };
    }

    private static bool StringEquals(string? left, string? right) => string.Equals(left, right, StringComparison.Ordinal);
}
