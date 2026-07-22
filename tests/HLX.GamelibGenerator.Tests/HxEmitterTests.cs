using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

public class HxEmitterTests
{
    [Fact]
    public void EmitClass_Widget_RealSetterOnlyProperty_RoutesThroughResolvedMember()
    {
        var src = HxEmitter.EmitClass(Fixture.FindClass("Widget"));
        Assert.Contains("public var x(get, set):Float;", src);
        Assert.Contains("inline function get_x():Float\n        return HlxRuntime.resolveField(this, \"x\");", src);
        Assert.Contains("static var __hlxMember_set_x:ResolvedMember;", src);
        Assert.Contains("HlxRuntime.resolveMember(HlxRuntime.resolveType(\"Widget\"), \"set_x\")", src);
    }

    [Fact]
    public void EmitClass_Widget_RealGetSetProperty_BothRouteThroughResolvedMember()
    {
        var src = HxEmitter.EmitClass(Fixture.FindClass("Widget"));
        Assert.Contains("HlxRuntime.resolveMember(HlxRuntime.resolveType(\"Widget\"), \"get_y\")", src);
        Assert.Contains("HlxRuntime.resolveMember(HlxRuntime.resolveType(\"Widget\"), \"set_y\")", src);
    }

    [Fact]
    public void EmitClass_Widget_ConstructorFactory_BakesRealFindex()
    {
        var w = Fixture.FindClass("Widget");
        var src = HxEmitter.EmitClass(w);
        Assert.Contains($"public inline function new(a0:Int, a1:String) {{", src);
        Assert.Contains($"this = HlxRuntime.constructInstance(t, {w.Constructor!.Findex}, [a0, a1]);", src);
    }

    [Fact]
    public void EmitClass_Sub_UsesFullyQualifiedPackagedNameInResolveCalls()
    {
        // Emitted-source-level pin of the same companion-naming bug ClassCollectorTests pins at the model level.
        var src = HxEmitter.EmitClass(Fixture.FindClass("fixture.pkg.Sub"));
        Assert.Contains("package fixture.pkg;", src);
        Assert.Contains("HlxRuntime.resolveType(\"fixture.pkg.Sub\")", src);
        Assert.DoesNotContain("$fixture.pkg.Sub", src);
    }

    [Fact]
    public void EmitClass_Dog_ChainsOntoAnimalWithForward()
    {
        var src = HxEmitter.EmitClass(Fixture.FindClass("Dog"));
        Assert.Contains("@:forward\nabstract Dog(Animal) from Animal to Animal {", src);
    }

    [Fact]
    public void EmitClass_Animal_HasNoAncestor_StaysDynamicBacked()
    {
        var src = HxEmitter.EmitClass(Fixture.FindClass("Animal"));
        Assert.Contains("abstract Animal(Dynamic) {", src);
        Assert.DoesNotContain("@:forward", src);
    }

    [Fact]
    public void EmitClass_NoGeneratedParent_OmitsForwardEvenWithFields()
    {
        var c = new GameClass { FullName = "Plain", TypeIndex = 0 };
        c.Fields.Add(new GameField { Name = "x", Type = new MappedType("Int", null), HasRealGetter = false, HasRealSetter = false });

        var src = HxEmitter.EmitClass(c);

        Assert.Contains("abstract Plain(Dynamic) {", src);
        Assert.DoesNotContain("@:forward", src);
    }

    [Fact]
    public void EmitClass_WithGeneratedParent_EmitsForwardChainHeader()
    {
        var c = new GameClass { FullName = "game.Child", TypeIndex = 0, ParentFullName = "game.Base" };

        var src = HxEmitter.EmitClass(c);

        Assert.Contains("@:forward\nabstract Child(game.Base) from game.Base to game.Base {", src);
    }

    [Fact]
    public void EmitClass_Sub_StaticRealAccessor_RoutesThroughStaticResolvedMember()
    {
        var src = HxEmitter.EmitClass(Fixture.FindClass("fixture.pkg.Sub"));
        Assert.Contains("HlxRuntime.resolveStaticMember(HlxRuntime.resolveType(\"fixture.pkg.Sub\"), \"get_ratio\")", src);
        Assert.Contains("HlxRuntime.resolveStaticMember(HlxRuntime.resolveType(\"fixture.pkg.Sub\"), \"set_ratio\")", src);
    }

    [Fact]
    public void EmitEnum_Direction_ZeroArgConstructors_AreStaticGetters()
    {
        var src = HxEmitter.EmitEnum(Fixture.FindEnum("Direction"));
        Assert.Contains("public static var North(get, never):Direction;", src);
        Assert.Contains("HlxRuntime.resolveStaticField(HlxRuntime.resolveType(\"Direction\"), \"North\")", src);
        Assert.Contains("public inline function isNorth():Bool\n        return getConstructorName() == \"North\";", src);
    }

    [Fact]
    public void EmitEnum_GameEvent_ParamTakingConstructor_IsAStaticFactoryMethod()
    {
        var src = HxEmitter.EmitEnum(Fixture.FindEnum("GameEvent"));
        Assert.Contains("public static function ScoreChanged(a0:Int, a1:String):GameEvent {", src);
        Assert.Contains("HlxRuntime.resolveStaticMember(HlxRuntime.resolveType(\"GameEvent\"), \"ScoreChanged\")", src);
        Assert.Contains("HlxRuntime.callResolved(__hlxEnumCtor_ScoreChanged, [a0, a1]);", src);
    }

    [Fact]
    public void EmitEnum_UsesStdQualifiedTypeForEnumIndexCalls()
    {
        // "std.Type", never bare "Type" - avoids shadowing by a generated wrapper literally named Type.
        var src = HxEmitter.EmitEnum(Fixture.FindEnum("Direction"));
        Assert.Contains("std.Type.enumIndex(cast this)", src);
        Assert.Contains("std.Type.enumConstructor(cast this)", src);
        Assert.Contains("std.Type.enumParameters(cast this)", src);
    }

    [Fact]
    public void EmitGenericGroup_ProducesGenericAbstractWithTParam()
    {
        var group = new GenericGroup
        {
            FullName = "hxbit.Weak",
            Instantiations = ["hxbit.Weak_Int", "hxbit.Weak_String"],
        };
        group.Fields.Add(new GameField { Name = "value", Type = new MappedType("T", null), HasRealGetter = false, HasRealSetter = false });

        var src = HxEmitter.EmitGenericGroup(group);

        Assert.Contains("package hxbit;", src);
        Assert.Contains("abstract Weak<T>(Dynamic) {", src);
        Assert.Contains("public var value(get, set):T;", src);
        Assert.Contains("Instantiations collapsed: hxbit.Weak_Int, hxbit.Weak_String", src);
        Assert.DoesNotContain("import hlx.runtime.ResolvedMember;", src);
    }

    [Fact]
    public void EmitGenericGroup_WithRealAccessor_ImportsResolvedMember()
    {
        var group = new GenericGroup { FullName = "Weak", Instantiations = ["Weak_Int", "Weak_String"] };
        group.Fields.Add(new GameField { Name = "value", Type = new MappedType("T", null), HasRealGetter = true, HasRealSetter = false });

        var src = HxEmitter.EmitGenericGroup(group);

        Assert.Contains("import hlx.runtime.ResolvedMember;", src);
        Assert.Contains("HlxRuntime.resolveMember(HlxRuntime.resolveType(\"Weak\"), \"get_value\")", src);
    }

    [Fact]
    public void EmitAlias_ProducesTypedefToParameterizedGroup()
    {
        var group = new GenericGroup { FullName = "hxbit.Weak", Instantiations = ["hxbit.Weak_Int"] };
        var src = HxEmitter.EmitAlias("hxbit.Weak_Int", group, "Int");

        Assert.Contains("package hxbit;", src);
        Assert.Contains("typedef Weak_Int = hxbit.Weak<Int>;", src);
    }

    [Fact]
    public void EmitClass_NoMethodsAndNoRealAccessors_OmitsResolvedMemberImport()
    {
        var c = new GameClass { FullName = "Plain", TypeIndex = 0 };
        c.Fields.Add(new GameField { Name = "x", Type = new MappedType("Int", null), HasRealGetter = false, HasRealSetter = false });

        var src = HxEmitter.EmitClass(c);

        Assert.DoesNotContain("import hlx.runtime.ResolvedMember;", src);
    }

    [Fact]
    public void EmitClass_WithOrdinaryMethod_ImportsResolvedMember()
    {
        var c = new GameClass { FullName = "Plain", TypeIndex = 0 };
        c.Methods.Add(new GameMethod { Name = "go", IsStatic = false, Params = [], Return = new MappedType("Void", null) });

        var src = HxEmitter.EmitClass(c);

        Assert.Contains("import hlx.runtime.ResolvedMember;", src);
    }
}
