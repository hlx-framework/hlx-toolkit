using System.IO;
using HLX.Analysis;
using HLX.Core;
using HLX.Core.IO;

namespace HLX.Analysis.Tests;

public class LoopForestTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hlboot.dat");

    private static HlModule LoadFixture()
    {
        using var fs = File.OpenRead(FixturePath);
        return HlReader.Read(fs);
    }

    [Fact]
    public void SimpleLoop_DetectsBackEdgeAndBody()
    {
        var cfg = SyntheticCfg.Build(0, (0, 1), (1, 2), (2, 1), (2, 3));
        var doms = DominatorTree.Compute(cfg);
        var loops = LoopForest.Build(cfg, doms);

        Assert.True(loops.LoopByHeader.ContainsKey(1));
        var loop = loops.LoopByHeader[1];
        Assert.Equal(new[] { 1, 2 }, loop.Body.OrderBy(x => x));
        Assert.Equal(new[] { 2 }, loop.BackEdgeSources.AsEnumerable());
        Assert.Equal(new[] { 2 }, loop.ExitBlocks.AsEnumerable());
    }

    [Fact]
    public void NoBackEdges_ProducesEmptyForest()
    {
        var cfg = SyntheticCfg.Build(0, (0, 1), (0, 2), (1, 3), (2, 3));
        var doms = DominatorTree.Compute(cfg);
        var loops = LoopForest.Build(cfg, doms);
        Assert.Empty(loops.LoopByHeader);
    }

    [Fact]
    public void IrreducibleCycle_DominanceFindsNoNaturalLoop()
    {
        // Irreducible graph (neither header dominates the other) - known, deliberate limitation.
        var cfg = SyntheticCfg.Build(0, (0, 1), (0, 2), (1, 2), (2, 1), (1, 3), (2, 3));
        var doms = DominatorTree.Compute(cfg);
        var loops = LoopForest.Build(cfg, doms);

        Assert.Empty(loops.LoopByHeader);
    }

    [Fact]
    public void NestedLoops_InnermostLoopContainingResolvesCorrectly()
    {
        var cfg = SyntheticCfg.Build(0,
            (0, 1), (1, 2), (2, 3), (3, 2), (3, 4), (4, 1), (4, 5));
        var doms = DominatorTree.Compute(cfg);
        var loops = LoopForest.Build(cfg, doms);

        Assert.True(loops.LoopByHeader.ContainsKey(1));
        Assert.True(loops.LoopByHeader.ContainsKey(2));

        var innermost = loops.InnermostLoopContaining(2);
        Assert.NotNull(innermost);
        Assert.Equal(2, innermost!.HeaderBlockId);
    }

    [Fact]
    public void KnownLoopFunction_HasTopLevelLoop()
    {
        var m = LoadFixture();
        var fn = m.Functions.First(f => f.FunctionIndex == 5); // String.indexOf
        var cfg = GraphBuilder.Build(fn);
        var doms = DominatorTree.Compute(cfg);
        var loops = LoopForest.Build(cfg, doms);
        Assert.NotEmpty(loops.LoopByHeader);
    }

    [Fact]
    public void Build_NeverThrowsForAnyFixtureFunction()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            var doms = DominatorTree.Compute(cfg);
            LoopForest.Build(cfg, doms);
        }
    }

    [Fact]
    public void NoOverlappingTopLevelLoops()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            var doms = DominatorTree.Compute(cfg);
            var loops = LoopForest.Build(cfg, doms).LoopByHeader.Values.ToList();

            for (int i = 0; i < loops.Count; i++)
            for (int j = i + 1; j < loops.Count; j++)
            {
                var a = loops[i];
                var b = loops[j];
                bool disjoint = !a.Body.Overlaps(b.Body);
                bool nested = a.Body.IsSubsetOf(b.Body) || b.Body.IsSubsetOf(a.Body);
                Assert.True(disjoint || nested,
                    $"fn#{fn.FunctionIndex}: loops headed at {a.HeaderBlockId} and {b.HeaderBlockId} partially overlap");
            }
        }
    }
}
