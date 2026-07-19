using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

// Fixture-driven tests cover the realistic paths; hand-built synthetic HlModules cover shapes
// that can't be forced through compiled Haxe source (ambiguous candidates, clobbered registers, etc).
public class ConstructorCollectorTests
{
    [Fact]
    public void Fixture_EveryNormalConstructor_ResolvesToADistinctFindex()
    {
        var ctors = Fixture.Get().Ctors;
        Assert.True(ctors.ClassesResolved >= 8);
        Assert.Equal(0, ctors.ClassesAmbiguous);
    }

    [Fact]
    public void Fixture_NeverInstantiatedClass_HasNoRecoveredFindex()
    {
        var (module, ctors, _, _, _, _) = Fixture.Get();
        var typeIndex = Array.FindIndex(module.Types.ToArray(), t => t is ObjectType o && o.Name == "NeverInstantiated");
        Assert.True(typeIndex >= 0);
        Assert.False(ctors.ConstructorFindexByTypeIndex.ContainsKey(typeIndex));
    }

    [Fact]
    public void AmbiguousCandidates_TwoDistinctFindexesForSameType_ResolvesNeither()
    {
        var objType = new ObjectType("Ambig", null, 0, [], [], []);
        var module = BuildModule(
            types: [new PrimitiveType(PrimitiveKind.Void), objType],
            functions:
            [
                MakeFunction(findex: 1, registers: [objType, new PrimitiveType(PrimitiveKind.Void)], instructions:
                [
                    NewInstr(dst: 0, offset: 0),
                    Call1Instr(result: 1, findex: 100, arg0: 0, offset: 1),
                ]),
                MakeFunction(findex: 2, registers: [objType, new PrimitiveType(PrimitiveKind.Void)], instructions:
                [
                    NewInstr(dst: 0, offset: 0),
                    Call1Instr(result: 1, findex: 200, arg0: 0, offset: 1),
                ]),
            ]);

        var collector = new ConstructorCollector(module);

        Assert.Equal(1, collector.ClassesAmbiguous);
        Assert.Equal(0, collector.ClassesResolved);
        Assert.False(collector.ConstructorFindexByTypeIndex.ContainsKey(1));
        Assert.Equal(2, collector.TotalCandidateSitesFound);
    }

    [Fact]
    public void RegisterClobberedBeforeCall_YieldsNoCandidate()
    {
        var objType = new ObjectType("Clobbered", null, 0, [], [], []);
        var module = BuildModule(
            types: [new PrimitiveType(PrimitiveKind.Void), objType],
            functions:
            [
                MakeFunction(findex: 1, registers: [objType, new PrimitiveType(PrimitiveKind.I32)], instructions:
                [
                    NewInstr(dst: 0, offset: 0),
                    // Overwrites reg 0 before any Call references it; the scan must give up, not attribute a later Call to this New.
                    new HlInstruction(HlOpcode.Mov, [0, 1], 1),
                    Call1Instr(result: 1, findex: 999, arg0: 0, offset: 2),
                ]),
            ]);

        var collector = new ConstructorCollector(module);

        Assert.Equal(0, collector.TotalCandidateSitesFound);
        Assert.False(collector.ConstructorFindexByTypeIndex.ContainsKey(1));
    }

    [Fact]
    public void UnrelatedInstructionsBetweenNewAndCall_DoNotDisqualifyTheMatch()
    {
        // Mirrors `new Foo(bar())`: a Call-family instruction that doesn't touch dstReg must not end the scan.
        var objType = new ObjectType("Nested", null, 0, [], [], []);
        var module = BuildModule(
            types: [new PrimitiveType(PrimitiveKind.Void), objType],
            functions:
            [
                MakeFunction(findex: 1, registers:
                [
                    objType,
                    new PrimitiveType(PrimitiveKind.I32),
                    new PrimitiveType(PrimitiveKind.I32),
                ], instructions:
                [
                    NewInstr(dst: 0, offset: 0),
                    // Evaluates a constructor argument, doesn't touch dstReg (0).
                    Call1Instr(result: 2, findex: 555, arg0: 1, offset: 1),
                    Call1Instr(result: 2, findex: 100, arg0: 0, offset: 2),
                ]),
            ]);

        var collector = new ConstructorCollector(module);

        Assert.Equal(1, collector.ClassesResolved);
        Assert.Equal(100, collector.ConstructorFindexByTypeIndex[1]);
    }

    private static HlInstruction NewInstr(int dst, int offset) =>
        new(HlOpcode.New, [dst], offset);

    private static HlInstruction Call1Instr(int result, int findex, int arg0, int offset) =>
        new(HlOpcode.Call1, [result, findex, arg0], offset);

    private static HlFunction MakeFunction(int findex, IEnumerable<HlType> registers, IEnumerable<HlInstruction> instructions) =>
        new(
            Type: new FunctionType([], 0),
            FunctionIndex: findex,
            Registers: [.. registers],
            Instructions: [.. instructions],
            DebugInfo: []);

    private static HlModule BuildModule(IEnumerable<HlType> types, IEnumerable<HlFunction> functions) =>
        new(
            Header: new HlHeader(4, HlFeatureFlags.None),
            Ints: [],
            Floats: [],
            Strings: [],
            Bytes: [],
            Types: [.. types],
            Natives: [],
            Functions: [.. functions],
            Globals: [],
            DebugFiles: [],
            EntryPoint: 0);
}
