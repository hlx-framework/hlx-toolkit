using System.IO;
using HLX.Analysis;
using HLX.Core;
using HLX.Core.IO;

namespace HLX.Analysis.Tests;

public class DominatorTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hlboot.dat");

    private static HlModule LoadFixture()
    {
        using var fs = File.OpenRead(FixturePath);
        return HlReader.Read(fs);
    }

    // Oracle: a dominates b iff removing a makes b unreachable from entry (or a == b).
    private static bool BruteForceDominates(ControlFlowGraph cfg, int a, int b)
    {
        if (a == b) return true;
        if (cfg.EntryBlockId == a) return true;
        if (cfg.EntryBlockId == b) return false;

        var visited = new HashSet<int> { cfg.EntryBlockId };
        var queue = new Queue<int>();
        queue.Enqueue(cfg.EntryBlockId);
        while (queue.Count > 0)
        {
            int n = queue.Dequeue();
            foreach (var e in cfg.Successors(n))
            {
                if (e.ToBlock == a) continue;
                if (visited.Add(e.ToBlock)) queue.Enqueue(e.ToBlock);
            }
        }
        return !visited.Contains(b);
    }

    private static void AssertMatchesBruteForce(ControlFlowGraph cfg)
    {
        var doms = DominatorTree.Compute(cfg);
        foreach (var a in cfg.Blocks)
        {
            if (!doms.IsReachable(a.Id)) continue;
            foreach (var b in cfg.Blocks)
            {
                if (!doms.IsReachable(b.Id)) continue;
                bool expected = BruteForceDominates(cfg, a.Id, b.Id);
                bool actual = doms.Dominates(a.Id, b.Id);
                Assert.True(expected == actual, $"Dominates({a.Id},{b.Id}) expected {expected}, got {actual}");
            }
        }
    }

    [Fact]
    public void Diamond_MatchesBruteForce()
    {
        var cfg = SyntheticCfg.Build(0, (0, 1), (0, 2), (1, 3), (2, 3));
        AssertMatchesBruteForce(cfg);
        var doms = DominatorTree.Compute(cfg);
        Assert.Equal(0, doms.ImmediateDominator(3)); // merge point's idom is the branch, not either arm
    }

    [Fact]
    public void SimpleLoop_MatchesBruteForce()
    {
        var cfg = SyntheticCfg.Build(0, (0, 1), (1, 2), (2, 1), (2, 3));
        AssertMatchesBruteForce(cfg);
        var doms = DominatorTree.Compute(cfg);
        Assert.Equal(1, doms.ImmediateDominator(2));
    }

    [Fact]
    public void NestedDiamond_MatchesBruteForce()
    {
        var cfg = SyntheticCfg.Build(0, (0, 1), (0, 2), (1, 3), (1, 4), (3, 5), (4, 5), (2, 5));
        AssertMatchesBruteForce(cfg);
    }

    [Fact]
    public void EntryDominatesAllReachableBlocks()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            if (cfg.Blocks.IsEmpty) continue;
            var doms = DominatorTree.Compute(cfg);
            foreach (var b in cfg.Blocks)
                if (doms.IsReachable(b.Id))
                    Assert.True(doms.Dominates(cfg.EntryBlockId, b.Id));
        }
    }

    [Fact]
    public void MatchesBruteForce_AcrossFixture()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            if (cfg.Blocks.IsEmpty || cfg.Blocks.Length > 60) continue; // keep runtime bounded
            AssertMatchesBruteForce(cfg);
        }
    }

    [Fact]
    public void PostDominatorTree_ExitPostDominatesReturnBlocks()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            if (cfg.Blocks.IsEmpty) continue;
            var pdoms = PostDominatorTree.Compute(cfg);
            foreach (var block in cfg.Blocks)
            {
                if (block.Instructions[^1].Opcode is HlOpcode.Ret)
                    Assert.True(pdoms.HasPostDominator(block.Id));
            }
        }
    }

    [Fact]
    public void PostDominatorTree_Diamond_MergeIsPostDominatedByExit()
    {
        var cfg = SyntheticCfg.Build(0, (0, 1), (0, 2), (1, 3), (2, 3));
        var pdoms = PostDominatorTree.Compute(cfg);
        Assert.Equal(3, pdoms.ImmediatePostDominator(0));
    }
}
