using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NuGetMcpServer.Models;

namespace NuGetMcpServer.Services;

/// <summary>
/// Compares types between two assemblies and detects changes
/// </summary>
public class TypeComparer
{
    private readonly DocumentationProvider _documentationProvider;

    public TypeComparer(DocumentationProvider documentationProvider)
    {
        _documentationProvider = documentationProvider;
    }

    /// <summary>
    /// Compare two types and detect all changes
    /// </summary>
    public List<TypeChange> CompareTypes(Type oldType, Type newType)
    {
        var changes = new List<TypeChange>();

        // Compare base class
        if (!AreTypesEquivalent(oldType.BaseType, newType.BaseType))
        {
            changes.Add(new TypeChange
            {
                Category = ChangeCategory.BaseClassChanged,
                Severity = ChangeSeverity.High,
                TypeName = newType.Name,
                TypeFullName = newType.FullName,
                FromType = oldType.BaseType != null ? GetTypeIdentifier(oldType.BaseType) : null,
                ToType = newType.BaseType != null ? GetTypeIdentifier(newType.BaseType) : null,
                Impact = "Base class change may break inheritance-based code",
                Migration = "Review derived classes and ensure compatibility with new base class"
            });
        }

        // Compare interfaces (using type identifier without version)
        var oldInterfacesDict = oldType.GetInterfaces().ToDictionary(i => GetTypeIdentifier(i), i => i);
        var newInterfacesDict = newType.GetInterfaces().ToDictionary(i => GetTypeIdentifier(i), i => i);

        foreach (var (identifier, interfaceType) in oldInterfacesDict)
        {
            if (!newInterfacesDict.ContainsKey(identifier))
            {
                changes.Add(new TypeChange
                {
                    Category = ChangeCategory.InterfaceRemoved,
                    Severity = ChangeSeverity.High,
                    TypeName = newType.Name,
                    TypeFullName = newType.FullName,
                    From = identifier,
                    Impact = $"Interface {identifier} removed - code expecting this interface will break",
                    Migration = $"Update code to not rely on {identifier} interface"
                });
            }
        }

        foreach (var (identifier, interfaceType) in newInterfacesDict)
        {
            if (!oldInterfacesDict.ContainsKey(identifier))
            {
                changes.Add(new TypeChange
                {
                    Category = ChangeCategory.InterfaceAdded,
                    Severity = ChangeSeverity.Low,
                    TypeName = newType.Name,
                    TypeFullName = newType.FullName,
                    To = identifier,
                    Impact = "New interface added - non-breaking change"
                });
            }
        }

        // Compare type modifiers
        if (!oldType.IsSealed && newType.IsSealed)
        {
            changes.Add(new TypeChange
            {
                Category = ChangeCategory.SealedAdded,
                Severity = ChangeSeverity.High,
                TypeName = newType.Name,
                TypeFullName = newType.FullName,
                Impact = "Type is now sealed - inheritance no longer possible",
                Migration = "Remove inheritance from this type or use composition"
            });
        }

        if (!oldType.IsAbstract && newType.IsAbstract)
        {
            changes.Add(new TypeChange
            {
                Category = ChangeCategory.AbstractAdded,
                Severity = ChangeSeverity.High,
                TypeName = newType.Name,
                TypeFullName = newType.FullName,
                Impact = "Type is now abstract - direct instantiation no longer possible",
                Migration = "Use derived concrete types instead"
            });
        }

        // Compare generic parameters
        var oldGenericArgs = oldType.GetGenericArguments();
        var newGenericArgs = newType.GetGenericArguments();

        if (oldGenericArgs.Length != newGenericArgs.Length)
        {
            changes.Add(new TypeChange
            {
                Category = ChangeCategory.GenericParametersChanged,
                Severity = ChangeSeverity.High,
                TypeName = newType.Name,
                TypeFullName = newType.FullName,
                From = $"{oldGenericArgs.Length} generic parameters",
                To = $"{newGenericArgs.Length} generic parameters",
                Impact = "Generic parameter count changed - breaks all usage",
                Migration = "Update all references to use new generic parameter signature"
            });
        }

        // Compare members
        if (oldType.IsEnum && newType.IsEnum)
        {
            changes.AddRange(CompareEnumValues(oldType, newType));
        }
        else
        {
            changes.AddRange(CompareMembers(oldType, newType));
        }

        return changes;
    }

    /// <summary>
    /// Compare properties and fields for type changes
    /// </summary>
    private List<TypeChange> ComparePropertiesAndFields(Type oldType, Type newType, BindingFlags flags)
    {
        var changes = new List<TypeChange>();

        // Compare properties
        var oldProps = oldType.GetProperties(flags).ToDictionary(p => p.Name);
        var newProps = newType.GetProperties(flags).ToDictionary(p => p.Name);

        foreach (var (name, oldProp) in oldProps)
        {
            if (newProps.TryGetValue(name, out var newProp))
            {
                if (!AreTypesEquivalent(oldProp.PropertyType, newProp.PropertyType))
                {
                    changes.Add(new TypeChange
                    {
                        Category = ChangeCategory.MemberTypeChanged,
                        Severity = ChangeSeverity.High,
                        TypeName = newType.Name,
                        TypeFullName = newType.FullName,
                        MemberName = name,
                        FromType = GetTypeIdentifier(oldProp.PropertyType),
                        ToType = GetTypeIdentifier(newProp.PropertyType),
                        From = $"public {oldProp.PropertyType.Name} {name} {{ get; set; }}",
                        To = $"public {newProp.PropertyType.Name} {name} {{ get; set; }}",
                        Impact = $"Property type changed from {oldProp.PropertyType.Name} to {newProp.PropertyType.Name}",
                        Migration = "Update code to handle new property type"
                    });
                }
            }
        }

        // Compare fields
        var oldFields = oldType.GetFields(flags).ToDictionary(f => f.Name);
        var newFields = newType.GetFields(flags).ToDictionary(f => f.Name);

        foreach (var (name, oldField) in oldFields)
        {
            if (newFields.TryGetValue(name, out var newField))
            {
                if (!AreTypesEquivalent(oldField.FieldType, newField.FieldType))
                {
                    changes.Add(new TypeChange
                    {
                        Category = ChangeCategory.MemberTypeChanged,
                        Severity = ChangeSeverity.High,
                        TypeName = newType.Name,
                        TypeFullName = newType.FullName,
                        MemberName = name,
                        FromType = GetTypeIdentifier(oldField.FieldType),
                        ToType = GetTypeIdentifier(newField.FieldType),
                        From = $"public {oldField.FieldType.Name} {name}",
                        To = $"public {newField.FieldType.Name} {name}",
                        Impact = $"Field type changed from {oldField.FieldType.Name} to {newField.FieldType.Name}",
                        Migration = "Update code to handle new field type"
                    });
                }
            }
        }

        return changes;
    }

    /// <summary>
    /// Detect when a type has been removed
    /// </summary>
    public TypeChange CreateTypeRemovedChange(Type type)
    {
        return new TypeChange
        {
            Category = ChangeCategory.TypeRemoved,
            Severity = ChangeSeverity.High,
            TypeName = type.Name,
            TypeFullName = type.FullName,
            Impact = "Type removed - all code using this type will break",
            Migration = "Remove references to this type or find alternative"
        };
    }

    /// <summary>
    /// Detect when a type has been added
    /// </summary>
    public TypeChange CreateTypeAddedChange(Type type)
    {
        var documentation = _documentationProvider.GetDocumentation(type);

        return new TypeChange
        {
            Category = ChangeCategory.TypeAdded,
            Severity = ChangeSeverity.Low,
            TypeName = type.Name,
            TypeFullName = type.FullName,
            Documentation = documentation,
            Impact = "New type added - non-breaking change"
        };
    }

    private List<TypeChange> CompareEnumValues(Type oldEnum, Type newEnum)
    {
        var changes = new List<TypeChange>();
        var oldValues = Enum.GetNames(oldEnum).ToHashSet();
        var newValues = Enum.GetNames(newEnum).ToHashSet();

        foreach (var removed in oldValues.Except(newValues))
        {
            changes.Add(new TypeChange
            {
                Category = ChangeCategory.EnumValueRemoved,
                Severity = ChangeSeverity.High,
                TypeName = newEnum.Name,
                TypeFullName = newEnum.FullName,
                MemberName = removed,
                Impact = $"Enum value {removed} removed - code using this value will break",
                Migration = $"Replace usage of {removed} with alternative value"
            });
        }

        foreach (var added in newValues.Except(oldValues))
        {
            var documentation = _documentationProvider.GetDocumentation(newEnum, added);

            changes.Add(new TypeChange
            {
                Category = ChangeCategory.EnumValueAdded,
                Severity = ChangeSeverity.Low,
                TypeName = newEnum.Name,
                TypeFullName = newEnum.FullName,
                MemberName = added,
                Documentation = documentation,
                Impact = "New enum value added - may require handling in switch statements"
            });
        }

        return changes;
    }

    private List<TypeChange> CompareMembers(Type oldType, Type newType)
    {
        var changes = new List<TypeChange>();

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Get all members
        var oldMembers = GetMembersDictionary(oldType, flags);
        var newMembers = GetMembersDictionary(newType, flags);

        // Find removed members
        foreach (var (signature, member) in oldMembers)
        {
            if (!newMembers.ContainsKey(signature))
            {
                changes.Add(new TypeChange
                {
                    Category = ChangeCategory.MemberRemoved,
                    Severity = ChangeSeverity.High,
                    TypeName = newType.Name,
                    TypeFullName = newType.FullName,
                    MemberName = member.Name,
                    From = signature,
                    Impact = $"Member {member.Name} removed - code using it will break",
                    Migration = $"Remove calls to {member.Name} or find alternative"
                });
            }
        }

        // Find added members
        foreach (var (signature, member) in newMembers)
        {
            if (!oldMembers.ContainsKey(signature))
            {
                var documentation = _documentationProvider.GetDocumentation(member);

                changes.Add(new TypeChange
                {
                    Category = ChangeCategory.MemberAdded,
                    Severity = ChangeSeverity.Low,
                    TypeName = newType.Name,
                    TypeFullName = newType.FullName,
                    MemberName = member.Name,
                    To = signature,
                    Documentation = documentation,
                    Impact = "New member added - non-breaking change"
                });
            }
        }

        // Find modified members (same name but different signature)
        var oldMethodsByName = oldType.GetMethods(flags).GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.ToList());
        var newMethodsByName = newType.GetMethods(flags).GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (name, oldMethods) in oldMethodsByName)
        {
            if (newMethodsByName.TryGetValue(name, out var newMethods))
            {
                // Check for changes in overloads
                changes.AddRange(CompareMethodOverloads(oldMethods, newMethods, newType));
            }
        }

        // Check for property and field type changes
        changes.AddRange(ComparePropertiesAndFields(oldType, newType, flags));

        // Post-process to detect compatible overloads (methods with optional parameters)
        DetectCompatibleOverloads(changes, oldType, newType, flags);

        return changes;
    }

    private List<TypeChange> CompareMethodOverloads(List<MethodInfo> oldMethods, List<MethodInfo> newMethods, Type type)
    {
        var changes = new List<TypeChange>();

        foreach (var oldMethod in oldMethods)
        {
            var oldSignature = GetMethodSignature(oldMethod);
            var matchingNew = newMethods.FirstOrDefault(m => GetMethodSignature(m) == oldSignature);

            if (matchingNew != null)
            {
                // Same signature exists - check for other changes
                if (!AreTypesEquivalent(oldMethod.ReturnType, matchingNew.ReturnType))
                {
                    changes.Add(new TypeChange
                    {
                        Category = ChangeCategory.ReturnTypeChanged,
                        Severity = ChangeSeverity.High,
                        TypeName = type.Name,
                        TypeFullName = type.FullName,
                        MemberName = oldMethod.Name,
                        FromType = oldMethod.ReturnType.Name,
                        ToType = matchingNew.ReturnType.Name,
                        Impact = "Return type changed - may break code expecting original type",
                        Migration = "Update code to handle new return type"
                    });
                }

                // Check for parameter changes
                var oldParams = oldMethod.GetParameters();
                var newParams = matchingNew.GetParameters();

                if (oldParams.Length != newParams.Length)
                {
                    changes.Add(new TypeChange
                    {
                        Category = ChangeCategory.MethodSignatureChanged,
                        Severity = ChangeSeverity.High,
                        TypeName = type.Name,
                        TypeFullName = type.FullName,
                        MemberName = oldMethod.Name,
                        From = GetMethodSignatureDetailed(oldMethod),
                        To = GetMethodSignatureDetailed(matchingNew),
                        Impact = "Method parameter count changed - breaks all calls",
                        Migration = "Update all method calls to match new signature"
                    });
                }
                else
                {
                    // Check for parameter type changes
                    for (int i = 0; i < oldParams.Length; i++)
                    {
                        if (!AreTypesEquivalent(oldParams[i].ParameterType, newParams[i].ParameterType))
                        {
                            changes.Add(new TypeChange
                            {
                                Category = ChangeCategory.ParameterTypeChanged,
                                Severity = ChangeSeverity.High,
                                TypeName = type.Name,
                                TypeFullName = type.FullName,
                                MemberName = oldMethod.Name,
                                From = $"{oldParams[i].Name}: {oldParams[i].ParameterType.Name}",
                                To = $"{newParams[i].Name}: {newParams[i].ParameterType.Name}",
                                FromType = GetTypeIdentifier(oldParams[i].ParameterType),
                                ToType = GetTypeIdentifier(newParams[i].ParameterType),
                                Impact = "Parameter type changed - breaks method calls",
                                Migration = "Update method calls to pass correct parameter type"
                            });
                        }
                    }
                }

                if (oldMethod.IsVirtual && !matchingNew.IsVirtual)
                {
                    changes.Add(new TypeChange
                    {
                        Category = ChangeCategory.VirtualRemoved,
                        Severity = ChangeSeverity.High,
                        TypeName = type.Name,
                        TypeFullName = type.FullName,
                        MemberName = oldMethod.Name,
                        Impact = "Method no longer virtual - overrides in derived classes will break",
                        Migration = "Remove overrides or use alternative extensibility pattern"
                    });
                }

                // Check accessibility
                if (GetAccessibilityLevel(oldMethod) > GetAccessibilityLevel(matchingNew))
                {
                    changes.Add(new TypeChange
                    {
                        Category = ChangeCategory.AccessibilityReduced,
                        Severity = ChangeSeverity.High,
                        TypeName = type.Name,
                        TypeFullName = type.FullName,
                        MemberName = oldMethod.Name,
                        Impact = "Method accessibility reduced - previously accessible code may break",
                        Migration = "Review access to this method"
                    });
                }
                else if (GetAccessibilityLevel(oldMethod) < GetAccessibilityLevel(matchingNew))
                {
                    changes.Add(new TypeChange
                    {
                        Category = ChangeCategory.AccessibilityExpanded,
                        Severity = ChangeSeverity.Low,
                        TypeName = type.Name,
                        TypeFullName = type.FullName,
                        MemberName = oldMethod.Name,
                        Impact = "Method accessibility expanded - non-breaking change"
                    });
                }

                // Check for obsolete attribute
                if (!oldMethod.GetCustomAttributes<ObsoleteAttribute>().Any() &&
                    matchingNew.GetCustomAttributes<ObsoleteAttribute>().Any())
                {
                    changes.Add(new TypeChange
                    {
                        Category = ChangeCategory.MemberObsoleted,
                        Severity = ChangeSeverity.Medium,
                        TypeName = type.Name,
                        TypeFullName = type.FullName,
                        MemberName = oldMethod.Name,
                        Impact = "Member marked as obsolete - should be replaced",
                        Migration = "Find replacement API as indicated by obsolete message"
                    });
                }
            }
        }

        return changes;
    }

    private Dictionary<string, MemberInfo> GetMembersDictionary(Type type, BindingFlags flags)
    {
        var members = new Dictionary<string, MemberInfo>();

        foreach (var method in type.GetMethods(flags).Where(m => !m.IsSpecialName))
        {
            var signature = GetMethodSignature(method);
            members[signature] = method;
        }

        foreach (var property in type.GetProperties(flags))
        {
            var signature = $"Property:{property.Name}:{property.PropertyType.Name}";
            members[signature] = property;
        }

        foreach (var field in type.GetFields(flags))
        {
            var signature = $"Field:{field.Name}:{field.FieldType.Name}";
            members[signature] = field;
        }

        foreach (var evt in type.GetEvents(flags))
        {
            var signature = $"Event:{evt.Name}";
            members[signature] = evt;
        }

        return members;
    }

    private string GetMethodSignature(MethodInfo method)
    {
        var sb = new StringBuilder();
        sb.Append(method.Name);
        sb.Append('(');

        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(parameters[i].ParameterType.Name);
        }

        sb.Append(')');
        return sb.ToString();
    }

    private string GetMethodSignatureDetailed(MethodInfo method)
    {
        var sb = new StringBuilder();
        sb.Append(method.Name);
        sb.Append('(');

        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"{parameters[i].ParameterType.Name} {parameters[i].Name}");
        }

        sb.Append(')');
        return sb.ToString();
    }

    private int GetAccessibilityLevel(MethodInfo method)
    {
        if (method.IsPublic) return 3;
        if (method.IsFamily) return 2;
        if (method.IsAssembly) return 1;
        return 0;
    }

    /// <summary>
    /// Compare types ignoring assembly version.
    /// This prevents false positives when only the assembly version changes (e.g., 22.6.0.0 → 22.7.0.0)
    /// but the actual type structure remains the same.
    /// </summary>
    private bool AreTypesEquivalent(Type? type1, Type? type2)
    {
        if (type1 == null && type2 == null) return true;
        if (type1 == null || type2 == null) return false;

        // For generic types, compare the generic type definition only (avoid recursion)
        if (type1.IsGenericType && type2.IsGenericType)
        {
            var def1 = type1.GetGenericTypeDefinition();
            var def2 = type2.GetGenericTypeDefinition();

            // Compare generic definitions by name and assembly
            if (def1.Namespace != def2.Namespace || def1.Name != def2.Name)
                return false;

            if (def1.Assembly.GetName().Name != def2.Assembly.GetName().Name)
                return false;

            // Compare number of generic arguments
            var args1 = type1.GetGenericArguments();
            var args2 = type2.GetGenericArguments();

            return args1.Length == args2.Length;
        }

        // For arrays, compare element types
        if (type1.IsArray && type2.IsArray)
        {
            return AreTypesEquivalent(type1.GetElementType(), type2.GetElementType());
        }

        // Compare namespace, name, and assembly name (without version)
        return type1.Namespace == type2.Namespace &&
               type1.Name == type2.Name &&
               type1.Assembly.GetName().Name == type2.Assembly.GetName().Name;
    }

    /// <summary>
    /// Get type identifier without assembly version (for comparison and display).
    /// Returns format: "Namespace.TypeName, AssemblyName" without version info.
    /// </summary>
    private string GetTypeIdentifier(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name;

        // Handle generic types
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();
            var argNames = string.Join(", ", genericArgs.Select(GetTypeIdentifier));
            return $"{genericDef.Namespace}.{genericDef.Name}[{argNames}], {assemblyName}";
        }

        return $"{type.FullName}, {assemblyName}";
    }

    /// <summary>
    /// Post-process changes to detect compatible method overloads (methods with optional parameters).
    /// Converts "MemberRemoved + MemberAdded" into "MethodOverloadAdded" when the new method
    /// is backward-compatible (same signature + optional parameters).
    /// </summary>
    private void DetectCompatibleOverloads(List<TypeChange> changes, Type oldType, Type newType, BindingFlags flags)
    {
        // Find all method-related removals and additions
        var removedMethods = changes
            .Where(c => c.Category == ChangeCategory.MemberRemoved && c.MemberName != null)
            .ToList();

        var addedMethods = changes
            .Where(c => c.Category == ChangeCategory.MemberAdded && c.MemberName != null)
            .ToList();

        var changesToRemove = new List<TypeChange>();
        var changesToAdd = new List<TypeChange>();

        // Process each removed method
        foreach (var removed in removedMethods)
        {
            // Find the old method by signature (use signature without parameter names)
            var oldMethod = oldType.GetMethods(flags)
                .FirstOrDefault(m => GetMethodSignature(m) == removed.From);
            if (oldMethod == null) continue;

            // Look for compatible new method with same name
            foreach (var added in addedMethods.Where(a => a.MemberName == removed.MemberName))
            {
                var newMethod = newType.GetMethods(flags)
                    .FirstOrDefault(m => GetMethodSignature(m) == added.To);
                if (newMethod == null) continue;

                // Check if this is a compatible overload
                if (IsCompatibleOverload(oldMethod, newMethod))
                {
                    // Mark for removal
                    changesToRemove.Add(removed);
                    changesToRemove.Add(added);

                    // Add replacement change
                    changesToAdd.Add(new TypeChange
                    {
                        Category = ChangeCategory.MethodOverloadAdded,
                        Severity = ChangeSeverity.Low,
                        TypeName = newType.Name,
                        TypeFullName = newType.FullName,
                        MemberName = newMethod.Name,
                        From = removed.From,
                        To = added.To,
                        Impact = "Compatible method overload added with optional parameters - non-breaking change",
                        Migration = "No changes required - existing calls will continue to work"
                    });

                    break; // Found match, move to next removed
                }
            }
        }

        // Apply changes
        foreach (var change in changesToRemove)
        {
            changes.Remove(change);
        }

        changes.AddRange(changesToAdd);
    }

    /// <summary>
    /// Check if a new method is a compatible overload of an old method.
    /// Compatible means: same return type, same first N parameters, and all additional parameters have default values.
    /// </summary>
    private bool IsCompatibleOverload(MethodInfo oldMethod, MethodInfo newMethod)
    {
        // Must have same return type
        if (!AreTypesEquivalent(oldMethod.ReturnType, newMethod.ReturnType))
            return false;

        var oldParams = oldMethod.GetParameters();
        var newParams = newMethod.GetParameters();

        // New method must have at least as many parameters
        if (newParams.Length < oldParams.Length)
            return false;

        // If same parameter count, not a compatible overload (likely a different kind of change)
        if (newParams.Length == oldParams.Length)
            return false;

        // First N parameters must match in type and position
        for (int i = 0; i < oldParams.Length; i++)
        {
            if (!AreTypesEquivalent(oldParams[i].ParameterType, newParams[i].ParameterType))
                return false;
        }

        // All additional parameters must have default values
        for (int i = oldParams.Length; i < newParams.Length; i++)
        {
            if (!newParams[i].HasDefaultValue)
                return false;
        }

        return true;
    }
}
