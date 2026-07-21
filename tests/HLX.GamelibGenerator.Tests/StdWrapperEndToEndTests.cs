using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

// End-to-end coverage of the bytecode-based std wrapper pipeline (ClassCollector /
// EnumCollector classification -> HaxeTypeMapper resolution -> Program.cs's rename
// step), against a small hand-built HlModule rather than the real fixture - exercises
// exactly the sequencing Program.cs itself runs, without needing a real hlboot.dat.
public class StdWrapperEndToEndTests
{
    private static HlModule MakeModule(IReadOnlyList<HlType> types) => new(
        Header: new HlHeader(4, HlFeatureFlags.None),
        Ints: [], Floats: [], Strings: [], Bytes: [],
        Types: [.. types],
        Natives: [], Functions: [], Globals: [], DebugFiles: [],
        EntryPoint: 0);

    // Mirrors Program.cs's own sequencing: classify -> build shared mapper off the union
    // of ordinary + std candidates -> CollectAll -> rename std wrapper candidates' output path.
    private static (ClassCollector Classes, EnumCollector Enums) Run(HlModule module)
    {
        var classes = new ClassCollector(module, new Dictionary<int, int>());
        var enums = new EnumCollector(module);
        var mapper = new HaxeTypeMapper(
            module,
            classes.CandidateNames.Union(classes.StdWrapperCandidateNames).ToHashSet(StringComparer.Ordinal),
            enums.CandidateNames.Union(enums.StdWrapperCandidateNames).ToHashSet(StringComparer.Ordinal));
        classes.CollectAll(mapper);
        enums.CollectAll(mapper);

        foreach (var c in classes.Classes)
            if (classes.StdWrapperCandidateNames.Contains(c.FullName))
            {
                c.RuntimeTypeName = c.FullName;
                c.FullName = Naming.StdWrapperPackagePrefix + c.FullName;
            }
        foreach (var e in enums.Enums)
            if (enums.StdWrapperCandidateNames.Contains(e.FullName))
            {
                e.RuntimeTypeName = e.FullName;
                e.FullName = Naming.StdWrapperPackagePrefix + e.FullName;
            }

        return (classes, enums);
    }

    [Fact]
    public void StdClassWithRealFields_IsClassifiedAsWrapperCandidate_AndRenamedOnOutput()
    {
        var types = new List<HlType>
        {
            new PrimitiveType(PrimitiveKind.I32),                                    // 0: Int
            new ObjectType("haxe.ds.StringMap", null, 0, [new HlField("h", 0)], [], []), // 1: real compiled shape
        };
        var (classes, _) = Run(MakeModule(types));

        Assert.Contains("haxe.ds.StringMap", classes.StdWrapperCandidateNames);
        Assert.DoesNotContain("haxe.ds.StringMap", classes.CandidateNames);

        var gc = classes.Classes.Single(c => c.RuntimeTypeName == "haxe.ds.StringMap");
        Assert.Equal("hlx.std.haxe.ds.StringMap", gc.FullName);
        Assert.Equal("hlx.std.haxe.ds", gc.Package);
        Assert.Equal("StringMap", gc.ShortName);
        Assert.Equal(["h"], gc.Fields.Select(f => f.Name));
    }

    [Fact]
    public void StdClassWithNoOwnMembersAndNoCompanion_IsNativeAbiShell_NeverGenerated()
    {
        var types = new List<HlType>
        {
            new ObjectType("sys.thread.Thread", null, 0, [], [], []),
        };
        var (classes, _) = Run(MakeModule(types));

        Assert.DoesNotContain("sys.thread.Thread", classes.StdWrapperCandidateNames);
        Assert.DoesNotContain("sys.thread.Thread", classes.CandidateNames);
        Assert.DoesNotContain(classes.Classes, c => c.RuntimeTypeName == "sys.thread.Thread" || c.FullName == "sys.thread.Thread");
    }

    [Fact]
    public void GameClass_FieldTypedAsStdWrapperCandidate_ResolvesToRenamedWrapperPath()
    {
        var types = new List<HlType>
        {
            new ObjectType("haxe.ds.StringMap", null, 0, [new HlField("h", 0)], [], []), // 0
            new ObjectType("game.Player", null, 0, [new HlField("lookup", 0)], [], []),   // 1: field typed as index 0
        };
        var (classes, _) = Run(MakeModule(types));

        var player = classes.Classes.Single(c => c.FullName == "game.Player");
        Assert.Equal("hlx.std.haxe.ds.StringMap", player.Field("lookup").Type.HaxeType);
    }

    [Fact]
    public void StdEnum_AlwaysClassifiedAsWrapperCandidate_AndRenamedOnOutput()
    {
        var types = new List<HlType>
        {
            new EnumType("haxe.io.Encoding", 0, [new HlEnumConstruct("UTF8", []), new HlEnumConstruct("RawNative", [])]),
        };
        var (_, enums) = Run(MakeModule(types));

        Assert.Contains("haxe.io.Encoding", enums.StdWrapperCandidateNames);
        var ge = enums.Enums.Single(e => e.RuntimeTypeName == "haxe.io.Encoding");
        Assert.Equal("hlx.std.haxe.io.Encoding", ge.FullName);
        Assert.Equal(["UTF8", "RawNative"], ge.Constructors.Select(c => c.Name));
    }

    [Fact]
    public void StdWrapperCandidate_CanReferenceAnotherStdWrapperCandidate()
    {
        // sys.io.FileInput.input : haxe.io.Input - both are wrap candidates from the same
        // module; classification (in each collector's own constructor) finishes before the
        // shared mapper is built, so build order of the two GameClasses can't matter.
        var types = new List<HlType>
        {
            new ObjectType("haxe.io.Input", null, 0, [new HlField("bigEndian", 0)], [], []),      // 0
            new ObjectType("sys.io.FileInput", null, 0, [new HlField("input", 0)], [], []),        // 1: field typed as index 0
        };
        var (classes, _) = Run(MakeModule(types));

        Assert.Contains("haxe.io.Input", classes.StdWrapperCandidateNames);
        Assert.Contains("sys.io.FileInput", classes.StdWrapperCandidateNames);

        var fileInput = classes.Classes.Single(c => c.RuntimeTypeName == "sys.io.FileInput");
        Assert.Equal("hlx.std.haxe.io.Input", fileInput.Field("input").Type.HaxeType);
    }

    [Fact]
    public void StdWrapperCandidate_ExtendingAnotherStdWrapperCandidate_ChainsParentFullNameToRenamedWrapperPath()
    {
        // ParentFullName must anticipate the parent's own "hlx.std.<name>" rename (applied
        // after CollectAll, in Program.cs) - referencing haxe.io.Output directly as this
        // abstract's underlying type would be the exact cross-module SafeCast bug the wrapper
        // exists to dodge (real bug found live: an EnumValueMap wrapper chained onto raw
        // haxe.ds.BalancedTree failed to even compile - "Not enough type parameters").
        var types = new List<HlType>
        {
            new ObjectType("haxe.io.Output", null, 0, [new HlField("bigEndian", 0)], [], []),                              // 0
            new ObjectType("sys.io.FileOutput", SuperIndex: 0, GlobalValue: 0, Fields: [new HlField("__f", 0)], Protos: [], Bindings: []), // 1: own field, so it's wrap-worthy on its own merits too
        };
        var (classes, _) = Run(MakeModule(types));

        var fileOutput = classes.Classes.Single(c => c.RuntimeTypeName == "sys.io.FileOutput");
        Assert.Equal("hlx.std.haxe.io.Output", fileOutput.ParentFullName);
    }

    [Fact]
    public void RootStdlibMagicName_NeverClassifiedAsWrapperCandidate()
    {
        var types = new List<HlType> { new ObjectType("Array", null, 0, [], [], []) };
        var (classes, _) = Run(MakeModule(types));

        Assert.DoesNotContain("Array", classes.StdWrapperCandidateNames);
        Assert.DoesNotContain("Array", classes.CandidateNames);
    }
}
