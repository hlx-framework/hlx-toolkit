using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

// The compiled fixture only exercises GenericGrouping's shadow guard (see ClassCollectorTests.Box_*);
// the positive collapse path and bail-out conditions are covered here with hand-built in-memory models.
public class GenericGroupingTests
{
    private static GameClass MakeClass(string fullName, params (string Name, string Type, bool RealGet, bool RealSet)[] fields)
    {
        var c = new GameClass { FullName = fullName, TypeIndex = 0 };
        foreach (var (name, type, realGet, realSet) in fields)
            c.Fields.Add(new GameField { Name = name, Type = new MappedType(type, null), HasRealGetter = realGet, HasRealSetter = realSet });
        return c;
    }

    [Fact]
    public void TwoInstantiations_WithVaryingFieldType_CollapseIntoOneGenericGroup()
    {
        var itemInt = MakeClass("Item_Int", ("value", "Int", false, false));
        var itemBool = MakeClass("Item_Bool", ("value", "Bool", false, false));

        var result = GenericGrouping.Run([itemInt, itemBool]);

        var group = Assert.Single(result.Groups);
        Assert.Equal("Item", group.FullName);
        Assert.Equal("value", Assert.Single(group.Fields).Name);
        Assert.Equal("T", Assert.Single(group.Fields).Type.HaxeType);
        Assert.Equal(
            new[] { "Item_Bool", "Item_Int" },
            group.Instantiations.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Empty(result.Singles);

        Assert.Equal(("Int"), result.Aliases["Item_Int"].TypeArg);
        Assert.Equal(("Bool"), result.Aliases["Item_Bool"].TypeArg);
        Assert.Same(group, result.Aliases["Item_Int"].Group);
    }

    [Fact]
    public void HasRealAccessor_OnlyPropagatedWhenAllInstantiationsAgree()
    {
        var a = MakeClass("Pair_Int", ("value", "Int", true, true));
        var b = MakeClass("Pair_Bool", ("value", "Bool", false, true));

        var result = GenericGrouping.Run([a, b]);

        var group = Assert.Single(result.Groups);
        var field = Assert.Single(group.Fields);
        Assert.False(field.HasRealGetter);
        Assert.True(field.HasRealSetter);
    }

    [Fact]
    public void HasRealAccessor_PropagatedWhenAllInstantiationsAgree()
    {
        var a = MakeClass("Solo_Int", ("value", "Int", true, true));
        var b = MakeClass("Solo_Bool", ("value", "Bool", true, true));

        var result = GenericGrouping.Run([a, b]);

        var field = Assert.Single(Assert.Single(result.Groups).Fields);
        Assert.True(field.HasRealGetter);
        Assert.True(field.HasRealSetter);
    }

    [Fact]
    public void MembersWithAnyMethod_AreNeverCollapsed()
    {
        var a = MakeClass("Widget_Int", ("value", "Int", false, false));
        var b = MakeClass("Widget_Bool", ("value", "Bool", false, false));
        a.Methods.Add(new GameMethod { Name = "touch", IsStatic = false, Params = [], Return = new MappedType("Void", null) });
        b.Methods.Add(new GameMethod { Name = "touch", IsStatic = false, Params = [], Return = new MappedType("Void", null) });

        var result = GenericGrouping.Run([a, b]);

        Assert.Empty(result.Groups);
        Assert.Equal(2, result.Singles.Count);
    }

    [Fact]
    public void NoVaryingPosition_IsNotCollapsed()
    {
        var a = MakeClass("Same_Int", ("value", "Int", false, false));
        var b = MakeClass("Same_Str", ("value", "Int", false, false));

        var result = GenericGrouping.Run([a, b]);

        Assert.Empty(result.Groups);
        Assert.Equal(2, result.Singles.Count);
    }

    [Fact]
    public void SingleMember_IsNeverCollapsed()
    {
        var a = MakeClass("Only_Int", ("value", "Int", false, false));
        var result = GenericGrouping.Run([a]);
        Assert.Empty(result.Groups);
        Assert.Single(result.Singles);
    }

    [Fact]
    public void MismatchedFieldShape_IsNotCollapsed()
    {
        var a = MakeClass("Mix_Int", ("value", "Int", false, false));
        var b = MakeClass("Mix_Bool", ("value", "Bool", false, false), ("extra", "String", false, false));

        var result = GenericGrouping.Run([a, b]);

        Assert.Empty(result.Groups);
        Assert.Equal(2, result.Singles.Count);
    }

    [Fact]
    public void MismatchedFieldNameAtSamePosition_IsNotCollapsed()
    {
        var a = MakeClass("Nm_Int", ("value", "Int", false, false));
        var b = MakeClass("Nm_Bool", ("other", "Bool", false, false));

        var result = GenericGrouping.Run([a, b]);

        Assert.Empty(result.Groups);
        Assert.Equal(2, result.Singles.Count);
    }

    [Fact]
    public void ExistingRealClassOfTheCollapsedBaseName_BlocksCollapsing()
    {
        // Mirrors the real fixture's Box/Box_Int/Box_String finding: a real class must never be shadowed by a collapse.
        var basePlaceholder = MakeClass("Boxed", ("value", "Dynamic", false, false));
        var a = MakeClass("Boxed_Int", ("value", "Int", false, false));
        var b = MakeClass("Boxed_Bool", ("value", "Bool", false, false));

        var result = GenericGrouping.Run([basePlaceholder, a, b]);

        Assert.Empty(result.Groups);
        Assert.Equal(3, result.Singles.Count);
    }

    [Fact]
    public void UnderscoreAtEndOrStart_IsNotTreatedAsAGenericSuffix()
    {
        var a = MakeClass("_Int", ("value", "Int", false, false));
        var b = MakeClass("Trailing_", ("value", "Int", false, false));

        var result = GenericGrouping.Run([a, b]);

        Assert.Empty(result.Groups);
        Assert.Equal(2, result.Singles.Count);
    }
}
