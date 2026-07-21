using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

// Synthetic coverage of HaxeTypeMapper edge cases hard to force through a real compiled program.
public class HaxeTypeMapperTests
{
    private static HaxeTypeMapper MakeMapper(
        IReadOnlyList<HlType> types,
        IReadOnlySet<string>? includedClasses = null,
        IReadOnlySet<string>? includedEnums = null)
    {
        var module = new HlModule(
            Header: new HlHeader(4, HlFeatureFlags.None),
            Ints: [],
            Floats: [],
            Strings: [],
            Bytes: [],
            Types: [.. types],
            Natives: [],
            Functions: [],
            Globals: [],
            DebugFiles: [],
            EntryPoint: 0);
        return new HaxeTypeMapper(module, includedClasses ?? new HashSet<string>(), includedEnums ?? new HashSet<string>());
    }

    [Theory]
    [InlineData(PrimitiveKind.Void, "Void")]
    [InlineData(PrimitiveKind.U8, "hl.UI8")]
    [InlineData(PrimitiveKind.U16, "hl.UI16")]
    [InlineData(PrimitiveKind.I32, "Int")]
    [InlineData(PrimitiveKind.I64, "haxe.Int64")]
    [InlineData(PrimitiveKind.F32, "hl.F32")]
    [InlineData(PrimitiveKind.F64, "Float")]
    [InlineData(PrimitiveKind.Bool, "Bool")]
    [InlineData(PrimitiveKind.Bytes, "hl.Bytes")]
    [InlineData(PrimitiveKind.Dyn, "Dynamic")]
    [InlineData(PrimitiveKind.Type, "hl.BaseType")]
    public void Map_Primitive_ProducesExpectedTypeWithNoFallback(PrimitiveKind kind, string expected)
    {
        var mapper = MakeMapper([]);
        var result = mapper.Map(new PrimitiveType(kind));
        Assert.Equal(expected, result.HaxeType);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public void Map_ArrayPrimitiveKind_FallsBackToDynamicWithReason()
    {
        // The raw HL "array" kind (distinct from a concrete ArrayObj/ArrayBytes_* ObjectType) has no element-type info.
        var mapper = MakeMapper([]);
        var result = mapper.Map(new PrimitiveType(PrimitiveKind.Array));
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void Map_DynObjAndGuid_FallBackToDynamicWithReason()
    {
        var mapper = MakeMapper([]);
        Assert.NotNull(mapper.Map(new PrimitiveType(PrimitiveKind.DynObj)).FallbackReason);
        Assert.NotNull(mapper.Map(new PrimitiveType(PrimitiveKind.Guid)).FallbackReason);
    }

    [Fact]
    public void Map_UnrecognizedPrimitiveKind_FallsBackToDynamic()
    {
        var mapper = MakeMapper([]);
        var result = mapper.Map(new PrimitiveType((PrimitiveKind)999));
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.Contains("999", result.FallbackReason);
    }

    [Fact]
    public void Map_VirtualType_NoFields_MapsToEmptyAnonStruct()
    {
        // An empty field list is a real (if unusual) Haxe anon-struct type, not a
        // "the generator gave up" case - no fields degraded, so no fallback reason.
        var mapper = MakeMapper([]);
        var result = mapper.Map(new VirtualType([]));
        Assert.Equal("{}", result.HaxeType);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public void Map_VirtualType_SimpleFields_MapsToAnonStruct()
    {
        // Real shape confirmed live for haxe.ds.StringMap.iterator(): { hasNext:()->Bool, next:()->Dynamic }.
        var types = new List<HlType>
        {
            new PrimitiveType(PrimitiveKind.Bool),                 // 0: Bool
            new FunctionType([], 0),                                // 1: () -> Bool
            new PrimitiveType(PrimitiveKind.Dyn),                  // 2: Dynamic
            new FunctionType([], 2),                                // 3: () -> Dynamic
            new VirtualType([new HlField("hasNext", 1), new HlField("next", 3)]), // 4
        };
        var mapper = MakeMapper(types);
        var result = mapper.Map(4);
        Assert.Equal("{ hasNext:() -> Bool, next:() -> Dynamic }", result.HaxeType);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public void Map_VirtualType_NestedVirtualTypeField_MapsRecursively()
    {
        // Real shape confirmed live for StringMap.keyValueIterator():
        // { hasNext:()->Bool, next:()->{ key:String, value:Dynamic } }.
        var types = new List<HlType>
        {
            new PrimitiveType(PrimitiveKind.Bool),                                    // 0: Bool
            new FunctionType([], 0),                                                   // 1: () -> Bool
            new ObjectType("String", null, 0, [], [], []),                             // 2: String
            new PrimitiveType(PrimitiveKind.Dyn),                                     // 3: Dynamic
            new VirtualType([new HlField("key", 2), new HlField("value", 3)]),         // 4: { key:String, value:Dynamic }
            new FunctionType([], 4),                                                   // 5: () -> { key:..., value:... }
            new VirtualType([new HlField("hasNext", 1), new HlField("next", 5)]),      // 6: outer
        };
        var mapper = MakeMapper(types);
        var result = mapper.Map(6);
        Assert.Equal("{ hasNext:() -> Bool, next:() -> { key:String, value:Dynamic } }", result.HaxeType);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public void Map_VirtualType_UnnameableField_FallsBackWholeTypeToDynamic()
    {
        // An Iterator missing `next` (silently dropped) would be worse than an honest
        // Dynamic - one bad field name takes down the WHOLE virtual type, not just itself.
        var types = new List<HlType>
        {
            new PrimitiveType(PrimitiveKind.Bool),
            new VirtualType([new HlField("", 0), new HlField("ok", 0)]),
        };
        var mapper = MakeMapper(types);
        var result = mapper.Map(1);
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void Map_VirtualType_ReservedWordFieldName_FallsBackWholeTypeToDynamic()
    {
        var types = new List<HlType>
        {
            new PrimitiveType(PrimitiveKind.Bool),
            new VirtualType([new HlField("class", 0)]), // "class" is a reserved word
        };
        var mapper = MakeMapper(types);
        var result = mapper.Map(1);
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void Map_VirtualType_SelfCyclicField_DoesNotHangOrCrash()
    {
        // A pathological virtual type whose own field points back at itself must degrade
        // gracefully (the cyclic field maps to Dynamic with a reason) instead of
        // recursing forever / stack-overflowing.
        var types = new List<HlType>
        {
            new VirtualType([new HlField("self", 0)]), // 0: field "self" points back at index 0
        };
        var mapper = MakeMapper(types);
        var result = mapper.Map(0);
        Assert.Equal("{ self:Dynamic }", result.HaxeType);
        Assert.NotNull(result.FallbackReason);
        Assert.Contains("cyclic", result.FallbackReason);
    }

    [Fact]
    public void Map_VirtualType_MutuallyCyclicChain_DoesNotHangOrCrash()
    {
        // Type 0 and type 1 are each VirtualTypes referencing each other via a
        // function-typed field - a longer cyclic chain than the direct self-reference case.
        var types = new List<HlType>
        {
            new FunctionType([], 1),                                    // 0: () -> (virtual at 1)
            new VirtualType([]),                                        // 1: placeholder, replaced below
        };
        // Build the real cycle: index 2 is a VirtualType with a field pointing to a
        // function (index 0) that returns back to a VirtualType at index 3, which in
        // turn has a field pointing to a function (index 4) returning to index 2.
        var cyclic = new List<HlType>
        {
            new FunctionType([], 3), // 0: () -> (virtual at 3)
            null!,                    // unused placeholder to keep indices readable
            new VirtualType([new HlField("a", 0)]), // 2
            new VirtualType([new HlField("b", 4)]), // 3
            new FunctionType([], 2), // 4: () -> (virtual at 2)
        };
        cyclic[1] = new PrimitiveType(PrimitiveKind.Void);
        var mapper = MakeMapper(cyclic);
        var result = mapper.Map(2);
        Assert.NotNull(result.HaxeType);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void Map_OutOfRangeTypeIndex_FallsBackToDynamic()
    {
        var mapper = MakeMapper([new PrimitiveType(PrimitiveKind.Void)]);
        var result = mapper.Map(42);
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.Contains("42", result.FallbackReason);
    }

    [Theory]
    [InlineData("_Main.Local")]  // leading-underscore segment - unreferenceable
    [InlineData("pkg.$Companion")] // "$" segment - unreferenceable
    [InlineData("hl_random")]    // not unreferenceable, but not a valid Haxe path either (lowercase leading char)
    public void Map_AbstractType_UnreferenceableOrInvalid_FallsBackToDynamic(string name)
    {
        var mapper = MakeMapper([]);
        var result = mapper.Map(new AbstractType(name));
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void Map_AbstractType_ValidReferenceablePath_IsUsedDirectly()
    {
        var mapper = MakeMapper([]);
        var result = mapper.Map(new AbstractType("hxsl.Type"));
        Assert.Equal("hxsl.Type", result.HaxeType);
        Assert.Null(result.FallbackReason);
    }

    [Theory]
    [InlineData("FMOD_STUDIO_EVENTDESCRIPTION")]
    [InlineData("FMOD_SYSTEM")]
    public void Map_AbstractType_FmodNativeAbiName_FallsBackToDynamic(string name)
    {
        // Bare native ABI marker types with no real Haxe declaration - see Naming.IsThirdPartyNativeAbiName.
        var mapper = MakeMapper([]);
        var result = mapper.Map(new AbstractType(name));
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void MapReferenceType_Null_WrapsInner()
    {
        var mapper = MakeMapper([new PrimitiveType(PrimitiveKind.I32)]);
        var result = mapper.Map(new ReferenceType(ReferenceKind.Null, 0));
        Assert.Equal("Null<Int>", result.HaxeType);
    }

    [Fact]
    public void MapReferenceType_Ref_WrapsInner()
    {
        var mapper = MakeMapper([new PrimitiveType(PrimitiveKind.I32)]);
        var result = mapper.Map(new ReferenceType(ReferenceKind.Ref, 0));
        Assert.Equal("hl.Ref<Int>", result.HaxeType);
    }

    [Fact]
    public void MapReferenceType_Packed_ApproximatesAsPlainTWithReason()
    {
        var mapper = MakeMapper([new PrimitiveType(PrimitiveKind.F64)]);
        var result = mapper.Map(new ReferenceType(ReferenceKind.Packed, 0));
        Assert.Equal("Float", result.HaxeType);
        Assert.Contains("packed<T>", result.FallbackReason);
    }

    [Fact]
    public void MapFunctionType_NestedReturnFunctionType_GetsExtraParens()
    {
        var types = new List<HlType>
        {
            new PrimitiveType(PrimitiveKind.Void),                 // 0: Void
            new FunctionType([], 0),                                // 1: () -> Void
            new FunctionType([], 1),                                // 2: () -> (() -> Void)
        };
        var mapper = MakeMapper(types);
        var result = mapper.Map(2);
        Assert.Equal("() -> (() -> Void)", result.HaxeType);
    }

    [Fact]
    public void MapFunctionType_NonNestedReturn_HasNoExtraParens()
    {
        var types = new List<HlType>
        {
            new PrimitiveType(PrimitiveKind.I32),   // 0: Int
            new FunctionType([0], 0),                // 1: (Int) -> Int
        };
        var mapper = MakeMapper(types);
        var result = mapper.Map(1);
        Assert.Equal("(Int) -> Int", result.HaxeType);
    }

    [Fact]
    public void MapObjectType_ArrayObjAndArrayDyn_MapToArrayDynamic()
    {
        var mapper = MakeMapper([]);
        Assert.Equal("Array<Dynamic>", mapper.Map(new ObjectType("hl.types.ArrayObj", null, 0, [], [], [])).HaxeType);
        Assert.Equal("Array<Dynamic>", mapper.Map(new ObjectType("hl.types.ArrayDyn", null, 0, [], [], [])).HaxeType);
    }

    [Fact]
    public void MapObjectType_ArrayBytesSpecialization_ExtractsElementType()
    {
        // Suffix with no underscore is used as-is; one containing an underscore has it turned back into '.'.
        var mapper = MakeMapper([]);
        Assert.Equal("Array<Int>", mapper.Map(new ObjectType("hl.types.ArrayBytes_Int", null, 0, [], [], [])).HaxeType);
        Assert.Equal("Array<hl.F32>", mapper.Map(new ObjectType("hl.types.ArrayBytes_hl_F32", null, 0, [], [], [])).HaxeType);
    }

    [Fact]
    public void MapObjectType_KnownModuleQualifiedSecondaryType_ResolvesRealPath()
    {
        var mapper = MakeMapper([]);
        var result = mapper.Map(new ObjectType("hl.Class", null, 0, [], [], []));
        Assert.Equal("hl.BaseType.Class", result.HaxeType);
    }

    [Fact]
    public void MapObjectType_StdWrapperCandidate_RoutesThroughGeneratedWrapper_NotRawErasedName()
    {
        // Real bug: haxe.ds.StringMap is an ordinary Haxe class recompiled fresh per module -
        // referencing it directly (even with erased Dynamic type args) fails a runtime SafeCast
        // whenever the value actually originates from the host process ("Can't cast
        // haxe.ds.StringMap to haxe.ds.StringMap"). A std name the classifier decided needs
        // wrapping (i.e. present in includedClassNames - see ClassCollector.StdWrapperCandidateNames)
        // routes to its "hlx.std.<name>" generated wrapper instead, same as every ordinary game
        // class already does for its own bare name.
        var mapper = MakeMapper([], includedClasses: new HashSet<string> { "haxe.ds.StringMap", "haxe.ds.ObjectMap" });
        Assert.Equal("hlx.std.haxe.ds.StringMap", mapper.Map(new ObjectType("haxe.ds.StringMap", null, 0, [], [], [])).HaxeType);
        Assert.Equal("hlx.std.haxe.ds.ObjectMap", mapper.Map(new ObjectType("haxe.ds.ObjectMap", null, 0, [], [], [])).HaxeType);
    }

    [Fact]
    public void MapObjectType_StdTypeNotInScope_ReferencedDirectly()
    {
        // Unlike the old JSON-driven pipeline (which fell back to Dynamic pending a human-run
        // --extract-std), a std name the classifier decided does NOT need wrapping (never added
        // to includedClassNames - a native-ABI shell, no real fields/methods of its own) is
        // simply safe to reference directly, same bucket as a root-magic name.
        var mapper = MakeMapper([]);
        var result = mapper.Map(new ObjectType("sys.io.File", null, 0, [], [], []));
        Assert.Equal("sys.io.File", result.HaxeType);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public void MapObjectType_StdCompilerMagicName_StillReferencedDirectly()
    {
        // A bare compiler-magic name (Array, String, Map, ...) is genuinely shared across
        // every module - still safe to reference directly, unlike ordinary std classes.
        var mapper = MakeMapper([]);
        var result = mapper.Map(new ObjectType("Array", null, 0, [], [], []));
        Assert.Equal("Array", result.HaxeType);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public void MapObjectType_DomkitGenerics_ErasesTypeArgsToDynamic()
    {
        // Real bug: farever-gamelib's h2d.Object.dom emitted bare `domkit.Properties` (missing required type param).
        var mapper = MakeMapper([]);
        Assert.Equal("domkit.Properties<Dynamic>", mapper.Map(new ObjectType("domkit.Properties", null, 0, [], [], [])).HaxeType);
        Assert.Equal("domkit.PropertyHandler<Dynamic, Dynamic>", mapper.Map(new ObjectType("domkit.PropertyHandler", null, 0, [], [], [])).HaxeType);
        Assert.Equal("domkit.Component<Dynamic, Dynamic>", mapper.Map(new ObjectType("domkit.Component", null, 0, [], [], [])).HaxeType);
    }

    [Fact]
    public void MapObjectType_DomkitGenerics_LocallyGenerated_UsesBareName()
    {
        // When domkit.Properties etc. are locally generated (non-generic wrappers), a reference must use the bare
        // name, not the KnownGenericArity-erased form, or the wrapper and referencing file disagree on arity.
        var mapper = MakeMapper([], includedClasses: new HashSet<string>
        {
            "domkit.Properties",
            "domkit.PropertyHandler",
            "domkit.Component",
        });
        Assert.Equal("domkit.Properties", mapper.Map(new ObjectType("domkit.Properties", null, 0, [], [], [])).HaxeType);
        Assert.Equal("domkit.PropertyHandler", mapper.Map(new ObjectType("domkit.PropertyHandler", null, 0, [], [], [])).HaxeType);
        Assert.Equal("domkit.Component", mapper.Map(new ObjectType("domkit.Component", null, 0, [], [], [])).HaxeType);
    }

    [Fact]
    public void MapObjectType_UnreferenceableSegment_FallsBackToDynamic()
    {
        var mapper = MakeMapper([]);
        var result = mapper.Map(new ObjectType("_Main.Local", null, 0, [], [], []));
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void MapObjectType_IncludedCandidateClass_ReferencedDirectly()
    {
        var mapper = MakeMapper([], includedClasses: new HashSet<string> { "game.Player" });
        var result = mapper.Map(new ObjectType("game.Player", null, 0, [], [], []));
        Assert.Equal("game.Player", result.HaxeType);
        Assert.Null(result.FallbackReason);
    }

    [Fact]
    public void MapObjectType_NotIncludedClass_FallsBackToDynamic()
    {
        var mapper = MakeMapper([], includedClasses: new HashSet<string>());
        var result = mapper.Map(new ObjectType("game.Filtered", null, 0, [], [], []));
        Assert.Equal("Dynamic", result.HaxeType);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void MapEnumType_IncludedCandidateEnum_ReferencedDirectly()
    {
        var mapper = MakeMapper([], includedEnums: new HashSet<string> { "game.State" });
        var result = mapper.Map(new EnumType("game.State", 0, []));
        Assert.Equal("game.State", result.HaxeType);
    }

    [Fact]
    public void MapEnumType_NotIncluded_FallsBackToDynamic()
    {
        var mapper = MakeMapper([], includedEnums: new HashSet<string>());
        var result = mapper.Map(new EnumType("game.Other", 0, []));
        Assert.Equal("Dynamic", result.HaxeType);
    }

    [Fact]
    public void MapEnumType_KnownModuleQualifiedStdlibEnum_ResolvesRealPath()
    {
        var mapper = MakeMapper([]);
        // XmlType is root-package in the real stdlib table (declared inside Xml.hx).
        var result = mapper.Map(new EnumType("XmlType", 0, []));
        Assert.Equal("Xml.XmlType", result.HaxeType);
    }

    [Fact]
    public void MapCallable_DropsSelfForInstanceMethod_KeepsForStatic()
    {
        var types = new List<HlType>
        {
            new PrimitiveType(PrimitiveKind.Void),  // 0
            new PrimitiveType(PrimitiveKind.I32),   // 1
        };
        var mapper = MakeMapper(types, includedClasses: new HashSet<string> { "game.Owner" });
        var ft = new FunctionType([2, 1], 0); // (Owner, Int) -> Void
        var typesWithSelf = new List<HlType>(types) { new ObjectType("game.Owner", null, 0, [], [], []) };
        var mapper2 = MakeMapper(typesWithSelf, includedClasses: new HashSet<string> { "game.Owner" });

        var (dropped, ret) = mapper2.MapCallable(ft, dropSelf: true);
        Assert.Equal(["Int"], dropped.Select(p => p.HaxeType));
        Assert.Equal("Void", ret.HaxeType);

        var (kept, _) = mapper2.MapCallable(ft, dropSelf: false);
        Assert.Equal(["game.Owner", "Int"], kept.Select(p => p.HaxeType));
    }
}
