using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

public class ClassCollectorTests
{
    [Fact]
    public void Widget_PlainInstanceField_HasNoRealAccessors()
    {
        var f = Fixture.FindClass("Widget").Field("plainField");
        Assert.Equal("Int", f.Type.HaxeType);
        Assert.False(f.HasRealGetter);
        Assert.False(f.HasRealSetter);
    }

    [Fact]
    public void Widget_DefaultSetProperty_HasRealSetterOnly()
    {
        var f = Fixture.FindClass("Widget").Field("x");
        Assert.Equal("Float", f.Type.HaxeType);
        Assert.False(f.HasRealGetter);
        Assert.True(f.HasRealSetter);
    }

    [Fact]
    public void Widget_GetSetProperty_HasBothRealAccessors()
    {
        // @:isVar keeps a physical field so get_y/set_y route through it; without it they'd be standalone methods.
        var f = Fixture.FindClass("Widget").Field("y");
        Assert.Equal("Float", f.Type.HaxeType);
        Assert.True(f.HasRealGetter);
        Assert.True(f.HasRealSetter);
    }

    [Fact]
    public void Widget_PlainStaticField_HasNoRealAccessors()
    {
        var f = Fixture.FindClass("Widget").StaticField("plainStatic");
        Assert.Equal("Int", f.Type.HaxeType);
        Assert.False(f.HasRealGetter);
        Assert.False(f.HasRealSetter);
    }

    [Fact]
    public void Widget_StaticDefaultSetProperty_HasRealSetterOnly()
    {
        var f = Fixture.FindClass("Widget").StaticField("resolution");
        Assert.False(f.HasRealGetter);
        Assert.True(f.HasRealSetter);
    }

    [Fact]
    public void Widget_StaticGetSetProperty_HasBothRealAccessors()
    {
        var f = Fixture.FindClass("Widget").StaticField("ratio");
        Assert.True(f.HasRealGetter);
        Assert.True(f.HasRealSetter);
    }

    [Fact]
    public void Widget_RoutedAccessorProtos_AreNotExposedAsStandaloneMethods()
    {
        var w = Fixture.FindClass("Widget");
        Assert.DoesNotContain(w.Methods, m => m.Name == "set_x");
        Assert.DoesNotContain(w.Methods, m => m.Name == "set_resolution");
        Assert.DoesNotContain(w.Methods, m => m.Name == "get_y");
        Assert.DoesNotContain(w.Methods, m => m.Name == "set_y");
        Assert.Contains(
            "instance method 'set_x': real compiled property accessor - routed through its field's own (get, set) wrapper property instead of a standalone method",
            w.Notes);
        Assert.Contains(
            "static method 'set_resolution': real compiled property accessor - routed through its static field's own (get, set) wrapper property instead of a standalone method",
            w.Notes);
    }

    [Fact]
    public void Widget_StaticMethod_IsMarkedStatic()
    {
        var m = Fixture.FindClass("Widget").Method("bump");
        Assert.True(m.IsStatic);
        Assert.Empty(m.Params);
        Assert.Equal("Int", m.Return.HaxeType);
    }

    [Fact]
    public void Widget_InstanceMethod_IsNotMarkedStatic()
    {
        var m = Fixture.FindClass("Widget").Method("describe");
        Assert.False(m.IsStatic);
    }

    [Fact]
    public void Widget_VoidToVoidCallbackField_MapsToParenthesizedArrow()
    {
        var f = Fixture.FindClass("Widget").Field("onClick");
        Assert.Equal("() -> Void", f.Type.HaxeType);
    }

    [Fact]
    public void Widget_CallbackReturningCallback_GetsExtraParens()
    {
        // Nested return-function-type needs an extra parens pair or the result isn't valid Haxe syntax.
        var m = Fixture.FindClass("Widget").Method("bind");
        var param = Assert.Single(m.Params);
        Assert.Equal("() -> (() -> Void)", param.HaxeType);
    }

    [Fact]
    public void Widget_OptionalIntParam_MapsToNullInt()
    {
        var m = Fixture.FindClass("Widget").Method("maybe");
        var param = Assert.Single(m.Params);
        Assert.Equal("Null<Int>", param.HaxeType);
    }

    [Fact]
    public void Widget_HlRefParam_MapsToHlRefType()
    {
        var m = Fixture.FindClass("Widget").Method("addInto");
        Assert.Equal("hl.Ref<Int>", m.Params[0].HaxeType);
        Assert.Equal("Int", m.Params[1].HaxeType);
    }

    [Fact]
    public void Widget_ArrayIntField_ResolvesToParameterizedArray()
    {
        var f = Fixture.FindClass("Widget").Field("tags");
        Assert.Equal("Array<Int>", f.Type.HaxeType);
        Assert.Null(f.Type.FallbackReason);
    }

    [Fact]
    public void Widget_MapField_RoutesThroughGeneratedStdWrapper()
    {
        // haxe.ds.StringMap is an ordinary Haxe class recompiled fresh per module - referencing
        // it directly fails a runtime SafeCast when the value originates from the host process.
        // KnownStdWrapperPaths routes it to a generated hlx.std.* wrapper instead (see
        // JsonStdApiExtractor/HaxeTypeMapperTests' own coverage of the underlying mapping rule).
        var f = Fixture.FindClass("Widget").Field("lookup");
        Assert.Equal("hlx.std.haxe.ds.StringMap", f.Type.HaxeType);
    }

    [Fact]
    public void Widget_UnambiguousConstructor_IsRecovered()
    {
        var w = Fixture.FindClass("Widget");
        Assert.NotNull(w.Constructor);
        Assert.Equal(["Int", "String"], w.Constructor!.Params.Select(p => p.HaxeType));
    }

    [Fact]
    public void NeverInstantiated_HasNoConstructor()
    {
        // Never `new`'d - ConstructorCollector's zero-candidate-sites path; must stay null, not a skip note.
        var c = Fixture.FindClass("NeverInstantiated");
        Assert.Null(c.Constructor);
        Assert.DoesNotContain(c.Notes, n => n.Contains("constructor"));
    }

    [Fact]
    public void NoExplicitCtor_StillGetsARecoveredConstructor()
    {
        // No `function new()` in source, but Haxe synthesizes its own ctor (field initializer after super()).
        var nc = Fixture.FindClass("NoExplicitCtor");
        var animal = Fixture.FindClass("Animal");
        Assert.NotNull(nc.Constructor);
        Assert.NotNull(animal.Constructor);
        Assert.NotEqual(animal.Constructor!.Findex, nc.Constructor!.Findex);
    }

    [Fact]
    public void Dog_ConstructorRecovery_NotConfusedWithSuperCall()
    {
        // Dog's `new` calls super(name) internally; must resolve to Dog's ctor, not Animal's.
        var dog = Fixture.FindClass("Dog");
        var animal = Fixture.FindClass("Animal");
        Assert.NotNull(dog.Constructor);
        Assert.NotEqual(animal.Constructor!.Findex, dog.Constructor!.Findex);
        Assert.Equal(["String", "String"], dog.Constructor!.Params.Select(p => p.HaxeType));
    }

    [Fact]
    public void ClassCollector_NeverFlattensAncestorFields()
    {
        // Ancestor member access goes through ParentFullName/@:forward chaining instead, not flattening.
        var dog = Fixture.FindClass("Dog");
        var animal = Fixture.FindClass("Animal");
        Assert.Equal(["breed"], dog.Fields.Select(f => f.Name));
        Assert.Equal(["name"], animal.Fields.Select(f => f.Name));
    }

    [Fact]
    public void Dog_ParentFullName_ResolvesToAnimal()
    {
        var dog = Fixture.FindClass("Dog");
        Assert.Equal("Animal", dog.ParentFullName);
    }

    [Fact]
    public void NoExplicitCtor_ParentFullName_ResolvesToAnimal()
    {
        var nc = Fixture.FindClass("NoExplicitCtor");
        Assert.Equal("Animal", nc.ParentFullName);
    }

    [Fact]
    public void Animal_ParentFullName_IsNull()
    {
        var animal = Fixture.FindClass("Animal");
        Assert.Null(animal.ParentFullName);
    }

    [Fact]
    public void Widget_ParentFullName_IsNull()
    {
        var widget = Fixture.FindClass("Widget");
        Assert.Null(widget.ParentFullName);
    }

    [Fact]
    public void Sub_FullNameAndPackage_AreCorrect()
    {
        var sub = Fixture.FindClass("fixture.pkg.Sub");
        Assert.Equal("fixture.pkg", sub.Package);
        Assert.Equal("Sub", sub.ShortName);
    }

    [Fact]
    public void Sub_StaticMembers_AreCollected()
    {
        // Regression: if the companion resolved as "$fixture.pkg.Sub" instead of "fixture.pkg.$Sub", this would silently find nothing.
        var sub = Fixture.FindClass("fixture.pkg.Sub");
        Assert.NotEmpty(sub.StaticFields);
        Assert.Contains(sub.StaticFields, f => f.Name == "counter");
        Assert.Contains(sub.Methods, m => m.Name == "increment" && m.IsStatic);
    }

    [Fact]
    public void Sub_PackagedRealAccessors_BothInstanceAndStatic()
    {
        var sub = Fixture.FindClass("fixture.pkg.Sub");

        var value = sub.Field("value");
        Assert.False(value.HasRealGetter);
        Assert.True(value.HasRealSetter);

        var ratio = sub.StaticField("ratio");
        Assert.True(ratio.HasRealGetter);
        Assert.True(ratio.HasRealSetter);

        var label = sub.Field("label");
        Assert.False(label.HasRealGetter);
        Assert.False(label.HasRealSetter);
    }

    [Fact]
    public void Sub_Constructor_IsRecovered()
    {
        var sub = Fixture.FindClass("fixture.pkg.Sub");
        Assert.NotNull(sub.Constructor);
        Assert.Equal(["String"], sub.Constructor!.Params.Select(p => p.HaxeType));
    }

    [Fact]
    public void CompanionType_UsesPackageDollarShortName_NotDollarPackage()
    {
        // Real companion is "fixture.pkg.$Sub"; the old buggy "$fixture.pkg.Sub" must never exist.
        var module = Fixture.Get().Module;
        var names = module.Types.OfType<ObjectType>().Select(o => o.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("fixture.pkg.$Sub", names);
        Assert.DoesNotContain("$fixture.pkg.Sub", names);
    }

    [Fact]
    public void EventBus_EnumTypedField_ResolvesToGeneratedEnumWrapper()
    {
        var bus = Fixture.FindClass("EventBus");
        Assert.Equal("GameEvent", bus.Field("lastEvent").Type.HaxeType);
    }

    [Fact]
    public void EventBus_MethodTakingEnumParam_ResolvesCorrectly()
    {
        var bus = Fixture.FindClass("EventBus");
        var m = bus.Method("fire");
        Assert.Equal("GameEvent", Assert.Single(m.Params).HaxeType);
        Assert.Equal("Void", m.Return.HaxeType);
    }

    [Fact]
    public void EventBus_StaticMethodReturningEnum_ResolvesCorrectly()
    {
        var bus = Fixture.FindClass("EventBus");
        var m = bus.Method("currentDirection");
        Assert.True(m.IsStatic);
        Assert.Equal("Direction", m.Return.HaxeType);
    }

    [Fact]
    public void Box_RawGenericTemplate_IsItsOwnClassWithDynamicField()
    {
        // Haxe still compiles the raw, unspecialized Box<T> even though nothing references it directly - this is what makes GenericGrouping's shadow guard fire below.
        var box = Fixture.FindClass("Box");
        var value = box.Field("value");
        Assert.Equal("Dynamic", value.Type.HaxeType);
        Assert.Null(value.Type.FallbackReason);
    }

    [Fact]
    public void Box_Instantiations_AreDistinctClassesWithConcreteFieldTypes()
    {
        var boxInt = Fixture.FindClass("Box_Int");
        var boxString = Fixture.FindClass("Box_String");
        Assert.Equal("Int", boxInt.Field("value").Type.HaxeType);
        Assert.Equal("String", boxString.Field("value").Type.HaxeType);
        Assert.NotNull(boxInt.Constructor);
        Assert.NotNull(boxString.Constructor);
        Assert.NotEqual(boxInt.Constructor!.Findex, boxString.Constructor!.Findex);
    }

    [Fact]
    public void CandidateNames_IncludePackagedClass_ExcludeCompanions()
    {
        var names = Fixture.Get().Classes.CandidateNames;
        Assert.Contains("fixture.pkg.Sub", names);
        Assert.Contains("Widget", names);
        Assert.DoesNotContain("fixture.pkg.$Sub", names);
        Assert.DoesNotContain("$Widget", names);
    }

    // Fixture never extends an excluded-namespace (haxe./hl./sys.) class, so this needs a hand-built module.
    [Fact]
    public void ExcludedNamespaceAncestor_HasARealSuperIndex_ButDoesNotChain()
    {
        var excludedAncestor = new ObjectType("haxe.io.Bytes", null, 0, [], [], []);
        var gameChild = new ObjectType("game.Child", SuperIndex: 0, 0, [], [], []);
        var module = new HlModule(
            Header: new HlHeader(4, HlFeatureFlags.None),
            Ints: [], Floats: [], Strings: [], Bytes: [],
            Types: [excludedAncestor, gameChild],
            Natives: [], Functions: [], Globals: [], DebugFiles: [],
            EntryPoint: 0);

        var collector = new ClassCollector(module, new Dictionary<int, int>());
        var mapper = new HaxeTypeMapper(module, collector.CandidateNames, new HashSet<string>());
        collector.CollectAll(mapper);

        var child = collector.Classes.Single(c => c.FullName == "game.Child");
        Assert.Null(child.ParentFullName);
    }
}
