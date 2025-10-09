using System;

namespace TestLibrary;

// === BREAKING CHANGES ===

// 1. ClassToRemove - DELETED

// 2. Member type changes (int → long) ✓
public class ClassWithMemberTypeChange
{
    public long Amount { get; set; }  // BREAKING: int → long
    public long? NullableAmount { get; set; }  // BREAKING: int? → long?
}

// 3. Member removed ✓
public class ClassWithMemberRemoval
{
    // MethodToRemove - DELETED
    // PropertyToRemove - DELETED
    // FieldToRemove - DELETED
}

// 4. Method signature changed ✓
public class ClassWithMethodSignatureChange
{
    public void MethodWithParamRemoval(int x) { }  // BREAKING: parameter removed
    public void MethodWithParamTypeChange(long x) { }  // BREAKING: int → long
    public long MethodWithReturnTypeChange() => 0;  // BREAKING: int → long
}

// 5. Virtual removed ✓
public class ClassWithVirtualRemoval
{
    public void VirtualMethod() { }  // BREAKING: no longer virtual
}

// 6. Base class changed ✓
public class ClassWithBaseChange : BaseClass2 { }  // BREAKING: BaseClass1 → BaseClass2

// 7. Interface removed ✓
public class ClassWithInterfaceRemoval : IInterface1 { }  // BREAKING: IInterface2 removed

// 8. Accessibility reduced ✓
public class ClassWithAccessibilityReduction
{
    internal void PublicMethod() { }  // BREAKING: public → internal
}

// 9. Sealed added ✓
public sealed class ClassToBeSealed { }  // BREAKING: sealed added

// 10. Abstract added ✓
public abstract class ClassToBeAbstract { }  // BREAKING: abstract added

// 11. Generic parameters changed ✓
public class GenericClass<T, U> { }  // BREAKING: <T> → <T, U>

// 12. Enum value removed ✓
public enum EnumWithValueRemoval
{
    Value1,
    // Value2 - DELETED
    Value3
}

// === NON-BREAKING CHANGES ===

// 13. Member obsoleted ✓
public class ClassWithObsolete
{
    [Obsolete("Use NewMethod instead")]
    public void MethodToObsolete() { }  // NON-BREAKING: [Obsolete] added
}

// 14. Accessibility expanded ✓
public class ClassWithAccessibilityExpansion
{
    public void InternalMethod() { }  // NON-BREAKING: internal → public
}

// 15. Parameter default changed ✓
public class ClassWithDefaultChange
{
    public void MethodWithDefault(int x = 10) { }  // NON-BREAKING: default 5 → 10
}

// === ADDITIONS ===

// 16. New members added ✓
public class ClassWithAdditions
{
    public void ExistingMethod() { }
    public void NewMethod() { }  // ADDITION
    public string NewProperty { get; set; } = "";  // ADDITION
    public int NewField;  // ADDITION
    public void ExistingMethod(int overload) { }  // ADDITION: method overload
    public void MethodWithOptionalParam(int x = 0) { }  // ADDITION: optional parameter
}

// 17. New interface added ✓
public interface INewInterface { }
public class ClassWithInterfaceAddition : INewInterface { }  // ADDITION

// 18. New enum value added ✓
public enum EnumWithValueAddition
{
    Value1,
    Value2,
    Value3  // ADDITION
}

// === NEW TYPES (ADDITIONS) ===
public class NewClass { }  // ADDITION
public interface IAddedInterface { }  // ADDITION
public enum NewEnum { Value1 }  // ADDITION
public delegate void NewDelegate(int x);  // ADDITION
public struct NewStruct { public int Value; }  // ADDITION

// === EXTENSION METHODS ===
public static class ExtensionMethods
{
    public static void NewExtension(this string s) { }  // ADDITION
}

// Keep existing from V1
public class BaseClass1 { }
public class BaseClass2 { }
public interface IInterface1 { }
public interface IInterface2 { }
public delegate void TestDelegate(int x);
public struct TestStruct { public int Value; }

// === MEMBER NAME FILTER TESTING ===

// 19. For testing memberNameFilter ✓
public class ClassForMemberFilter
{
    // BREAKING: int → long
    public long StarCount { get; set; }     // CHANGED: int → long
    public int MessageCount { get; set; }   // unchanged

    // BREAKING: int → string
    public string TopicId = "";             // CHANGED: int → string
    public string UserId { get; set; } = "";  // unchanged

    // BREAKING: method removed
    // CalculateTotal() REMOVED
    public void CalculateSum() { }          // unchanged

    // BREAKING: return type changed
    public int GetValue() => 0;             // CHANGED: string → int
    public void GetData() { }               // unchanged

    // BREAKING: property removed
    // Amount property REMOVED
}

// 20. Another class to test memberFilter across types ✓
public class AnotherClassWithMembers
{
    public long StarCount { get; set; }  // CHANGED: int → long
    public void CalculateTotal() { }     // unchanged
}

// === OPTIONAL PARAMETERS TESTING ===

// 21. Compatible overload - method with optional parameters added (NON-breaking) ✓
public class ClassWithOptionalParamsAdded
{
    // V2: Method with optional parameter added - COMPATIBLE with V1 (non-breaking)
    public void SendMessage(string text, int? threadId = null) { }

    // V2: Method with multiple optional params added - COMPATIBLE (non-breaking)
    public int Calculate(int a, int b, int c = 0, int d = 0) => a + b + c + d;

    // V2: Method with optional param - COMPATIBLE (non-breaking)
    public string FormatText(string input, bool uppercase = false) =>
        uppercase ? input.ToUpper() : input;
}

// 22. Incompatible change - method with required parameters added (BREAKING) ✓
public class ClassWithRequiredParamsAdded
{
    // V2: Method with required parameter added - INCOMPATIBLE (breaking)
    public void ProcessData(int value, string mode) { }  // BREAKING: requires 'mode'
}

// 23. True new overload - old signature remains (NON-breaking) ✓
public class ClassWithTrueOverload
{
    // V2: Old signature STILL exists
    public void Execute(string command) { }

    // V2: NEW overload added (non-breaking)
    public void Execute(string command, int timeout) { }
}

// 24. Method parameter reordering (BREAKING) ✓
public class ClassWithParamReordering
{
    // V2: params reordered - BREAKING
    public void ConfigureSettings(int value, string name, bool enabled) { }
}
