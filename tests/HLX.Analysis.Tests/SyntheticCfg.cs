using System.Collections.Immutable;
using HLX.Analysis;
using HLX.Core;

namespace HLX.Analysis.Tests;

// Hand-built CFGs for shapes the fixture may not exercise; instruction content is irrelevant here.
internal static class SyntheticCfg
{
    private static readonly HlFunction DummyFunction = new(new FunctionType([], 0), 0, [], [], []);

    public static ControlFlowGraph Build(int entry, params (int From, int To, EdgeKind Kind)[] edges)
    {
        var blockIds = edges.SelectMany(e => new[] { e.From, e.To }).Append(entry).Distinct().OrderBy(x => x);
        var blocks = blockIds.Select(id => new BasicBlock(id, id, id + 1, [])).ToImmutableArray();
        var cfgEdges = edges.Select(e => new CfgEdge(e.From, e.To, e.Kind)).ToImmutableArray();
        return new ControlFlowGraph(DummyFunction, blocks, cfgEdges, entry);
    }

    public static ControlFlowGraph Build(int entry, params (int From, int To)[] edges) =>
        Build(entry, edges.Select(e => (e.From, e.To, EdgeKind.Jump)).ToArray());
}
