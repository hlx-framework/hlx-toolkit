namespace HLX.Analysis;

public sealed class ControlFlowGraph
{
    public HlFunction Function { get; }
    public ImmutableArray<BasicBlock> Blocks { get; }
    public int EntryBlockId { get; }

    private readonly ImmutableDictionary<int, ImmutableArray<CfgEdge>> _succ;
    private readonly ImmutableDictionary<int, ImmutableArray<CfgEdge>> _pred;
    private readonly ImmutableDictionary<int, int> _blockByOffset;

    internal ControlFlowGraph(
        HlFunction function,
        ImmutableArray<BasicBlock> blocks,
        ImmutableArray<CfgEdge> edges,
        int entryBlockId)
    {
        Function = function;
        Blocks = blocks;
        EntryBlockId = entryBlockId;

        var succBuilder = new Dictionary<int, List<CfgEdge>>();
        var predBuilder = new Dictionary<int, List<CfgEdge>>();
        foreach (var e in edges)
        {
            if (!succBuilder.TryGetValue(e.FromBlock, out var s)) succBuilder[e.FromBlock] = s = [];
            s.Add(e);
            if (!predBuilder.TryGetValue(e.ToBlock, out var p)) predBuilder[e.ToBlock] = p = [];
            p.Add(e);
        }
        _succ = succBuilder.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutableArray());
        _pred = predBuilder.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutableArray());

        var offsetBuilder = new Dictionary<int, int>();
        foreach (var b in blocks)
            for (int o = b.Start; o < b.End; o++)
                offsetBuilder[o] = b.Id;
        _blockByOffset = offsetBuilder.ToImmutableDictionary();
    }

    public ImmutableArray<CfgEdge> Successors(int blockId) =>
        _succ.TryGetValue(blockId, out var e) ? e : ImmutableArray<CfgEdge>.Empty;

    public ImmutableArray<CfgEdge> Predecessors(int blockId) =>
        _pred.TryGetValue(blockId, out var e) ? e : ImmutableArray<CfgEdge>.Empty;

    public BasicBlock BlockAt(int instructionOffset) => Blocks[_blockByOffset[instructionOffset]];

    public BasicBlock Block(int id) => Blocks[id];
}
