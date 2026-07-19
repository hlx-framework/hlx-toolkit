using System.IO;
using HLX.Analysis;
using HLX.Core;
using HLX.Core.IO;

namespace HLX.Analysis.Tests;

public class ControlFlowGraphTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hlboot.dat");

    private static HlModule LoadFixture()
    {
        using var fs = File.OpenRead(FixturePath);
        return HlReader.Read(fs);
    }

    [Fact]
    public void Build_AllInstructionsCoveredExactlyOnce()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            var covered = new bool[fn.Instructions.Length];
            foreach (var block in cfg.Blocks)
            {
                for (int o = block.Start; o < block.End; o++)
                {
                    Assert.False(covered[o], $"fn#{fn.FunctionIndex} offset {o} covered by more than one block");
                    covered[o] = true;
                }
            }
            Assert.All(covered, c => Assert.True(c));
        }
    }

    [Fact]
    public void Build_EveryNonEntryBlockHasAtLeastOnePredecessor()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            foreach (var block in cfg.Blocks)
            {
                if (block.Id == cfg.EntryBlockId) continue;
                Assert.True(cfg.Predecessors(block.Id).Length > 0,
                    $"fn#{fn.FunctionIndex} block {block.Id} (offset {block.Start}) has no predecessors");
            }
        }
    }

    [Fact]
    public void Build_TerminatorEdgeCountMatchesOpcodeShape()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            var cfg = GraphBuilder.Build(fn);
            foreach (var block in cfg.Blocks)
            {
                var last = block.Instructions[^1];
                int succCount = cfg.Successors(block.Id).Length;
                switch (last.Opcode)
                {
                    case HlOpcode.Ret:
                    case HlOpcode.Throw:
                    case HlOpcode.Rethrow:
                        Assert.Equal(0, succCount);
                        break;
                    case HlOpcode.JAlways:
                        Assert.Equal(1, succCount);
                        break;
                    case HlOpcode.JTrue:
                    case HlOpcode.JFalse:
                    case HlOpcode.JNull:
                    case HlOpcode.JNotNull:
                    case HlOpcode.JSLt:
                    case HlOpcode.JSGte:
                    case HlOpcode.JSGt:
                    case HlOpcode.JSLte:
                    case HlOpcode.JULt:
                    case HlOpcode.JUGte:
                    case HlOpcode.JNotLt:
                    case HlOpcode.JNotGte:
                    case HlOpcode.JEq:
                    case HlOpcode.JNotEq:
                        Assert.Equal(2, succCount);
                        break;
                }
            }
        }
    }

    [Fact]
    public void Build_EntryBlockContainsOffsetZero()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
        {
            if (fn.Instructions.IsEmpty) continue;
            var cfg = GraphBuilder.Build(fn);
            Assert.Equal(0, cfg.Block(cfg.EntryBlockId).Start);
        }
    }

    [Fact]
    public void Build_NeverThrowsForAnyFixtureFunction()
    {
        var m = LoadFixture();
        foreach (var fn in m.Functions)
            GraphBuilder.Build(fn);
    }
}
