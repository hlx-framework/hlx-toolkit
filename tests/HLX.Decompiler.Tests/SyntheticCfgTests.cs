namespace HLX.Decompiler.Tests;

// Hand-built: covers the "Trap with no matching EndTrap" fallback, since every real Trap in the fixture is well-formed.
public class SyntheticCfgTests
{
    private static readonly HlType I32 = new PrimitiveType(PrimitiveKind.I32);
    private static readonly HlType Dyn = new PrimitiveType(PrimitiveKind.Dyn);

    private static AstBlock Structure(HlFunction fn, HlModule module)
    {
        var cfg = GraphBuilder.Build(fn);
        var doms = DominatorTree.Compute(cfg);
        var pdoms = PostDominatorTree.Compute(cfg);
        var loops = LoopForest.Build(cfg, doms);
        var lowering = new IrLowering(fn, module, new Dictionary<int, string>());
        return AstBuilder.StructureFunction(cfg, doms, pdoms, loops, lowering);
    }

    private static IEnumerable<AstStatement> Flatten(AstStatement stmt)
    {
        yield return stmt;
        var children = stmt switch
        {
            AstBlock b => b.Statements.AsEnumerable(),
            AstIf i => i.Then.Statements.Concat(i.Else?.Statements ?? []),
            AstWhile w => w.Body.Statements.AsEnumerable(),
            AstSwitch s => s.Cases.SelectMany(c => c.Body.Statements),
            AstTry t => t.TryBody.Statements.Concat(t.Catch.Body.Statements),
            _ => []
        };
        foreach (var c in children)
        foreach (var d in Flatten(c))
            yield return d;
    }

    // Int r0,0; Trap r1,@3; Ret r0; Ret r1 (handler) - no EndTrap, so the shape is unrecognized.
    private static (HlFunction fn, HlModule module) BuildMalformedTrapFunction()
    {
        var instructions = ImmutableArray.Create(
            new HlInstruction(HlOpcode.Int, [0, 0], 0),
            new HlInstruction(HlOpcode.Trap, [1, 1], 1),
            new HlInstruction(HlOpcode.Ret, [0], 2),
            new HlInstruction(HlOpcode.Ret, [1], 3)
        );
        var fn = new HlFunction(new FunctionType([], 0), 0, [I32, Dyn], instructions, []);
        var module = new HlModule(
            new HlHeader(5, HlFeatureFlags.None), [0], [], [], [], [], [], [fn], [], [], 0);
        return (fn, module);
    }

    [Fact]
    public void MalformedTrap_FallsBackToCommentWithoutThrowing()
    {
        var (fn, module) = BuildMalformedTrapFunction();
        var ast = Structure(fn, module);
        var all = Flatten(ast).ToList();

        Assert.Contains(all, s => s is AstComment { Text: var t } && t.StartsWith("try (unrecognized"));
    }

    [Fact]
    public void MalformedTrap_StillCoversBothReturns()
    {
        var (fn, module) = BuildMalformedTrapFunction();
        var ast = Structure(fn, module);
        var all = Flatten(ast).ToList();

        Assert.Contains(all, s => s is AstLeaf { Ir: ReturnStatement { Value: RegisterExpr { Register: 0 } } });
        Assert.Contains(all, s => s is AstLeaf { Ir: ReturnStatement { Value: RegisterExpr { Register: 1 } } });
    }
}
