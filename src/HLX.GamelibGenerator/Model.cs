namespace HLX.GamelibGenerator;

// Type string to emit, plus (on a Dynamic fallback) a reason surfaced as a trailing
// comment so "erased on purpose" is distinguishable from "generator gave up".
internal readonly record struct MappedType(string HaxeType, string? FallbackReason)
{
    public static implicit operator string(MappedType m) => m.HaxeType;
}

// An ordinary instance field, always emitted as (get, set) - HL bytecode has no
// const/readonly marker to justify (get, never). HasRealGetter/HasRealSetter: a real
// compiled get_/set_ proto (e.g. h2d.Object.set_x, which also flips posChanged = true)
// gets routed through resolveMember/callResolved instead of raw field reflection, to
// preserve that side effect.
internal sealed class GameField
{
    public required string Name;
    public required MappedType Type;
    public bool HasRealGetter;
    public bool HasRealSetter;
}

internal sealed class GameMethod
{
    public required string Name;
    public required bool IsStatic;
    // Parameter names are synthesized (a0, a1, ...) - HL function types carry argument
    // TYPES only, no source names.
    public required IReadOnlyList<MappedType> Params;
    public required MappedType Return;
}

// A companion-type field with no Bindings entry - a plain static DATA field (vs.
// GameMethod's IsStatic=true, a static FUNCTION). Static-side twin of GameField:
// a real compiled static property (e.g. ui.Options.set_resolution) shows up as both
// this data field and a separately bound set_/get_ companion field.
internal sealed class GameStaticField
{
    public required string Name;
    public required MappedType Type;
    public bool HasRealGetter;
    public bool HasRealSetter;
}

// The real constructor findex, recovered via ConstructorCollector's bytecode scan.
// Params excludes the implicit receiver, same as GameMethod's instance methods.
internal sealed class GameConstructor
{
    public required int Findex;
    public required IReadOnlyList<MappedType> Params;
}

internal sealed class GameClass
{
    public required string FullName;   // real, dotted HL/Haxe name, e.g. "ui.MenuUI"
    public required int TypeIndex;
    public List<GameField> Fields { get; } = [];
    public List<GameMethod> Methods { get; } = [];
    public List<GameStaticField> StaticFields { get; } = [];
    public List<string> Notes { get; } = []; // skipped-member reasons, kept for reporting
    public GameConstructor? Constructor;

    // Direct ancestor's full name, set only when it also gets a generated wrapper
    // (see ClassCollector.ResolveGeneratedParent). Null means HxEmitter falls back
    // to plain `abstract X(Dynamic)` instead of chaining via @:forward.
    public string? ParentFullName;

    public string ShortName => Naming.ShortName(FullName);
    public string Package => Naming.PackageOf(FullName);
}

// One HL enum constructor. Index matches its position in EnumType.Constructs, the
// same position Type.enumIndex() reads at runtime - so isXxx() compares by index, never by name.
internal sealed class GameEnumConstructor
{
    public required string Name;
    public required int Index;
    public required IReadOnlyList<MappedType> ParamTypes;
}

internal sealed class GameEnum
{
    public required string FullName;   // real, dotted HL/Haxe name, e.g. "ui.MenuState"
    public required int TypeIndex;
    public List<GameEnumConstructor> Constructors { get; } = [];
    public List<string> Notes { get; } = []; // skipped-member reasons, kept for reporting

    public string ShortName => Naming.ShortName(FullName);
    public string Package => Naming.PackageOf(FullName);
}

// A collapsed @:generic monomorphization group - one emitted `abstract Base<T>`
// standing in for N structurally-identical concrete ObjectTypes that only differ in
// the type(s) substituted at the positions carrying the type parameter.
internal sealed class GenericGroup
{
    public required string FullName; // package + base short name, e.g. "hxbit.Weak"
    public List<GameField> Fields { get; } = [];
    public List<GameMethod> Methods { get; } = [];
    public required IReadOnlyList<string> Instantiations; // real concrete class names collapsed into this group

    public string ShortName => Naming.ShortName(FullName);
    public string Package => Naming.PackageOf(FullName);
}
