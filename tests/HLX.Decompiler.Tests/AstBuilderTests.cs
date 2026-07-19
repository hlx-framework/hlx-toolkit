namespace HLX.Decompiler.Tests;

public class AstBuilderTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hlboot.dat");

    private static HlModule LoadFixture()
    {
        using var fs = File.OpenRead(FixturePath);
        return HlReader.Read(fs);
    }

    private static Dictionary<int, string> BuildFuncNames(HlModule module)
    {
        var names = new Dictionary<int, string>();
        foreach (var type in module.Types)
            if (type is ObjectType obj)
                foreach (var proto in obj.Protos)
                    names.TryAdd(proto.FunctionIndex, $"{obj.Name}::{proto.Name}");
        return names;
    }

    private static AstBlock Structure(HlFunction fn, HlModule module, IReadOnlyDictionary<int, string> funcNames)
    {
        var cfg = GraphBuilder.Build(fn);
        var doms = DominatorTree.Compute(cfg);
        var pdoms = PostDominatorTree.Compute(cfg);
        var loops = LoopForest.Build(cfg, doms);
        var lowering = new IrLowering(fn, module, funcNames);
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

    [Fact]
    public void StructureFunction_NeverThrows_AcrossFixture()
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        foreach (var fn in m.Functions)
            Structure(fn, m, funcNames);
    }

    [Fact]
    public void StructureFunction_CoversEveryInstruction_AcrossFixture()
    {
        // Only counts instructions IrLowering.Lower maps to non-null; nothing else should be silently dropped.
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            var lowering = new IrLowering(fn, m, funcNames);
            int expected = fn.Instructions.Count(i => lowering.Lower(i) is not null);

            var ast = Structure(fn, m, funcNames);
            int actual = Flatten(ast).OfType<AstLeaf>().Count();

            Assert.True(expected == actual,
                $"fn#{fn.FunctionIndex}: expected {expected} lowered statements, AST has {actual}");
        }
    }

    [Fact]
    public void KnownIfElseFunction_ProducesIfWithNoGoto()
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        var fn = m.Functions.First(f => f.FunctionIndex == 277); // StringBuf::add
        var ast = Structure(fn, m, funcNames);
        var all = Flatten(ast).ToList();

        Assert.Contains(all, s => s is AstIf);
        Assert.DoesNotContain(all, s => s is AstGoto);
        Assert.DoesNotContain(all, s => s is AstLabel);
    }

    [Theory]
    [InlineData(4)] // String::findChar — single clean loop, break inside body
    [InlineData(5)] // String::indexOf — guard clauses + an internal loop
    public void KnownLoopFunction_ProducesWhileWithNoGoto(int findex)
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        var fn = m.Functions.First(f => f.FunctionIndex == findex);
        var ast = Structure(fn, m, funcNames);
        var all = Flatten(ast).ToList();

        Assert.Contains(all, s => s is AstWhile);
        Assert.DoesNotContain(all, s => s is AstGoto);
        Assert.DoesNotContain(all, s => s is AstLabel);
    }

    [Fact]
    public void KnownSwitchFunction_ProducesSwitchStatement()
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        var fn = m.Functions.First(f => f.FunctionIndex == 261); // Std::__compare (nested Switch dispatch)
        var ast = Structure(fn, m, funcNames);
        var all = Flatten(ast).ToList();

        Assert.Contains(all, s => s is AstSwitch);
    }

    [Fact]
    public void KnownTryCatchFunction_ProducesTryStatement()
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        var fn = m.Functions.First(f => f.FunctionIndex == 63); // ArrayObj::toString (real Trap/EndTrap)
        var ast = Structure(fn, m, funcNames);
        var all = Flatten(ast).ToList();

        var tryStmt = Assert.IsType<AstTry>(all.First(s => s is AstTry));
        Assert.Contains(Flatten(tryStmt.Catch.Body), s => s is AstLeaf { Ir: ThrowStatement { IsRethrow: true } });
        Assert.DoesNotContain(all, s => s is AstGoto);
        Assert.DoesNotContain(all, s => s is AstLabel);
        Assert.DoesNotContain(all, s => s is AstComment { Text: var t } && t.StartsWith("try (unrecognized"));
    }

    [Fact]
    public void GotoFallbackUsage_IsRareAcrossFixture()
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        int withGoto = 0;
        foreach (var fn in m.Functions)
        {
            var ast = Structure(fn, m, funcNames);
            if (Flatten(ast).Any(s => s is AstGoto))
                withGoto++;
        }
        // Canary, not a hard gate - some goto fallback is expected until switch/try structuring lands.
        Assert.True(withGoto < m.Functions.Length / 2,
            $"{withGoto}/{m.Functions.Length} functions fell back to goto — investigate before proceeding");
    }
}
