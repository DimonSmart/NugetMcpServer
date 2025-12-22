namespace TestLibrary;

// === BREAKING CHANGES ===

// 1. Type will be REMOVED in V2
public class ClassToRemove
{
    public void SomeMethod() { }
}

// 2. Member type changes (int → long)
public class ClassWithMemberTypeChange
{
    public int Amount { get; set; }
    public int? NullableAmount { get; set; }
}

// 3. Member removed
public class ClassWithMemberRemoval
{
    public void MethodToRemove() { }
    public string PropertyToRemove { get; set; } = "";
    public int FieldToRemove;
}

// 4. Method signature changed
public class ClassWithMethodSignatureChange
{
    public void MethodWithParamRemoval(int x, int y) { }
    public void MethodWithParamTypeChange(int x) { }
    public int MethodWithReturnTypeChange() => 0;
}

// 5. Virtual removed
public class ClassWithVirtualRemoval
{
    public virtual void VirtualMethod() { }
}

// 6. Base class changed
public class BaseClass1 { }
public class BaseClass2 { }
public class ClassWithBaseChange : BaseClass1 { }

// 7. Interface removed
public interface IInterface1 { }
public interface IInterface2 { }
public class ClassWithInterfaceRemoval : IInterface1, IInterface2 { }

// 8. Accessibility reduced (public → internal)
public class ClassWithAccessibilityReduction
{
    public void PublicMethod() { }
}

// 9. Sealed added
public class ClassToBeSealed { }

// 10. Abstract added
public class ClassToBeAbstract { }

// 11. Generic parameters changed
public class GenericClass<T> { }

// 12. Enum value removed
public enum EnumWithValueRemoval
{
    Value1,
    Value2,  // Will be removed in V2
    Value3
}

// === NON-BREAKING CHANGES ===

// 13. Member obsoleted
public class ClassWithObsolete
{
    public void MethodToObsolete() { }
}

// 14. Accessibility expanded (internal → public)
public class ClassWithAccessibilityExpansion
{
    internal void InternalMethod() { }
}

// 15. Parameter default changed
public class ClassWithDefaultChange
{
    public void MethodWithDefault(int x = 5) { }
}

// === ADDITIONS ===

// 16. New members will be added
public class ClassWithAdditions
{
    public void ExistingMethod() { }
}

// 17. New interface will be added
public class ClassWithInterfaceAddition { }

// 18. New enum value will be added
public enum EnumWithValueAddition
{
    Value1,
    Value2
}

// === DELEGATES ===
public delegate void TestDelegate(int x);

// === STRUCTS ===
public struct TestStruct
{
    public int Value;
}

// === MEMBER NAME FILTER TESTING ===

// 19. For testing memberNameFilter with wildcards and OR
public class ClassForMemberFilter
{
    // Fields ending with "Count" - testing *Count wildcard
    public int StarCount { get; set; }
    public int MessageCount { get; set; }

    // Fields ending with "Id" - testing *Id wildcard
    public int TopicId;
    public string UserId { get; set; } = "";

    // Methods starting with "Calculate" - testing Calculate* wildcard
    public int CalculateTotal() => 0;
    public void CalculateSum() { }

    // Methods starting with "Get" - testing Get* wildcard
    public string GetValue() => "";
    public void GetData() { }

    // Property for exact match testing
    public string Amount { get; set; } = "";
}

// 20. Another class to test that memberFilter works across types
public class AnotherClassWithMembers
{
    public int StarCount { get; set; }  // Same name as in ClassForMemberFilter
    public void CalculateTotal() { }    // Same name as in ClassForMemberFilter
}

// === OPTIONAL PARAMETERS TESTING ===

// 21. Compatible overload - method with optional parameters added (NON-breaking)
public class ClassWithOptionalParamsAdded
{
    // V1: Simple method
    public void SendMessage(string text) { }

    // V1: Method with two params
    public int Calculate(int a, int b) => a + b;

    // V1: Method with return type
    public string FormatText(string input) => input;
}

// 22. Incompatible change - method with required parameters added (BREAKING)
public class ClassWithRequiredParamsAdded
{
    // V1: Simple method - will be REPLACED with version requiring more params
    public void ProcessData(int value) { }
}

// 23. True new overload - old signature remains (NON-breaking)
public class ClassWithTrueOverload
{
    // V1: This will remain in V2, but additional overload will be added
    public void Execute(string command) { }
}

// 24. Method parameter reordering (BREAKING)
public class ClassWithParamReordering
{
    // V1: params in one order
    public void ConfigureSettings(string name, int value, bool enabled) { }
}
