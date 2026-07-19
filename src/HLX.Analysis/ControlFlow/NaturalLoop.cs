namespace HLX.Analysis;

// Found via back-edge dominance closure, so loops here are always reducible;
// irreducible regions simply don't surface as a NaturalLoop.
public sealed record NaturalLoop(
    int HeaderBlockId,
    ImmutableHashSet<int> Body,
    ImmutableArray<int> BackEdgeSources,
    ImmutableArray<int> ExitBlocks
);

public sealed class LoopForest
{
    public ImmutableDictionary<int, NaturalLoop> LoopByHeader { get; }

    private LoopForest(ImmutableDictionary<int, NaturalLoop> loopByHeader) => LoopByHeader = loopByHeader;

    public NaturalLoop? InnermostLoopContaining(int blockId)
    {
        NaturalLoop? best = null;
        foreach (var loop in LoopByHeader.Values)
        {
            if (!loop.Body.Contains(blockId)) continue;
            if (best is null || loop.Body.Count < best.Body.Count) best = loop;
        }
        return best;
    }

    public static LoopForest Build(ControlFlowGraph cfg, DominatorTree doms)
    {
        var tailsByHeader = new Dictionary<int, HashSet<int>>();
        foreach (var block in cfg.Blocks)
        {
            if (!doms.IsReachable(block.Id)) continue;
            foreach (var edge in cfg.Successors(block.Id))
            {
                if (doms.IsReachable(edge.ToBlock) && doms.Dominates(edge.ToBlock, block.Id))
                {
                    if (!tailsByHeader.TryGetValue(edge.ToBlock, out var tails)) tailsByHeader[edge.ToBlock] = tails = [];
                    tails.Add(block.Id);
                }
            }
        }

        var loops = ImmutableDictionary.CreateBuilder<int, NaturalLoop>();
        foreach (var (header, tails) in tailsByHeader)
        {
            var body = new HashSet<int> { header };
            var worklist = new Stack<int>();
            foreach (int t in tails)
                if (body.Add(t)) worklist.Push(t);

            while (worklist.Count > 0)
            {
                int n = worklist.Pop();
                foreach (var edge in cfg.Predecessors(n))
                    if (body.Add(edge.FromBlock))
                        worklist.Push(edge.FromBlock);
            }

            var exitBlocks = body
                .Where(b => cfg.Successors(b).Any(e => !body.Contains(e.ToBlock)))
                .ToImmutableArray();

            loops[header] = new NaturalLoop(
                header,
                body.ToImmutableHashSet(),
                tails.OrderBy(x => x).ToImmutableArray(),
                exitBlocks);
        }

        return new LoopForest(loops.ToImmutable());
    }
}
