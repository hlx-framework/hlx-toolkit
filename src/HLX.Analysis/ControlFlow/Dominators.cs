namespace HLX.Analysis;

// Iterative dominator computation (Cooper-Harvey-Kennedy); parameterized over
// preds/succs so PostDominatorTree can reuse it on the reversed graph.
internal static class DominatorAlgorithm
{
    public static ImmutableDictionary<int, int> ComputeIdom(
        int root, Func<int, IReadOnlyList<int>> preds, Func<int, IReadOnlyList<int>> succs)
    {
        var order = ReversePostorder(root, succs);
        var rpoNumber = new Dictionary<int, int>();
        for (int i = 0; i < order.Count; i++) rpoNumber[order[i]] = i;

        var idom = new Dictionary<int, int> { [root] = root };
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (int b in order)
            {
                if (b == root) continue;
                int? newIdom = null;
                foreach (int p in preds(b))
                {
                    if (!idom.ContainsKey(p)) continue;
                    newIdom = newIdom is null ? p : Intersect(newIdom.Value, p, idom, rpoNumber);
                }
                if (newIdom is null) continue;
                if (!idom.TryGetValue(b, out int cur) || cur != newIdom.Value)
                {
                    idom[b] = newIdom.Value;
                    changed = true;
                }
            }
        }
        return idom.ToImmutableDictionary();
    }

    private static int Intersect(int a, int b, Dictionary<int, int> idom, Dictionary<int, int> rpo)
    {
        while (a != b)
        {
            while (rpo[a] > rpo[b]) a = idom[a];
            while (rpo[b] > rpo[a]) b = idom[b];
        }
        return a;
    }

    private static List<int> ReversePostorder(int root, Func<int, IReadOnlyList<int>> succs)
    {
        var order = new List<int>();
        var visited = new HashSet<int> { root };
        var stack = new Stack<(int Node, IEnumerator<int> Next)>();
        stack.Push((root, succs(root).GetEnumerator()));
        while (stack.Count > 0)
        {
            var (node, it) = stack.Peek();
            if (it.MoveNext())
            {
                int next = it.Current;
                if (visited.Add(next))
                    stack.Push((next, succs(next).GetEnumerator()));
            }
            else
            {
                order.Add(node);
                stack.Pop();
            }
        }
        order.Reverse();
        return order;
    }
}

public sealed class DominatorTree
{
    private readonly ImmutableDictionary<int, int> _idom;
    private readonly ImmutableDictionary<int, ImmutableArray<int>> _children;
    public int Root { get; }

    private DominatorTree(int root, ImmutableDictionary<int, int> idom, ImmutableDictionary<int, ImmutableArray<int>> children)
    {
        Root = root;
        _idom = idom;
        _children = children;
    }

    public bool IsReachable(int blockId) => _idom.ContainsKey(blockId);

    public int ImmediateDominator(int blockId) => _idom[blockId];

    public bool Dominates(int a, int b)
    {
        int cur = b;
        while (true)
        {
            if (cur == a) return true;
            if (cur == Root) return false;
            cur = ImmediateDominator(cur);
        }
    }

    public ImmutableArray<int> Children(int blockId) =>
        _children.TryGetValue(blockId, out var c) ? c : ImmutableArray<int>.Empty;

    public static DominatorTree Compute(ControlFlowGraph cfg)
    {
        var idom = DominatorAlgorithm.ComputeIdom(
            cfg.EntryBlockId,
            b => cfg.Predecessors(b).Select(e => e.FromBlock).Distinct().ToArray(),
            b => cfg.Successors(b).Select(e => e.ToBlock).Distinct().ToArray());
        return new DominatorTree(cfg.EntryBlockId, idom, BuildChildren(cfg.EntryBlockId, idom));
    }

    private static ImmutableDictionary<int, ImmutableArray<int>> BuildChildren(int root, ImmutableDictionary<int, int> idom)
    {
        var builder = new Dictionary<int, List<int>>();
        foreach (var (node, parent) in idom)
        {
            if (node == root) continue;
            if (!builder.TryGetValue(parent, out var list)) builder[parent] = list = [];
            list.Add(node);
        }
        return builder.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutableArray());
    }
}

// Post-dominance relative to a synthetic virtual exit node (VirtualExitId), since
// post-dominance requires a single, unique exit but a function can have several.
public sealed class PostDominatorTree
{
    public const int VirtualExitId = -1;

    private readonly ImmutableDictionary<int, int> _ipdom;

    private PostDominatorTree(ImmutableDictionary<int, int> ipdom) => _ipdom = ipdom;

    public bool HasPostDominator(int blockId) => _ipdom.ContainsKey(blockId);

    public int ImmediatePostDominator(int blockId) => _ipdom[blockId];

    public bool PostDominates(int a, int b)
    {
        int cur = b;
        while (true)
        {
            if (cur == a) return true;
            if (cur == VirtualExitId) return false;
            cur = ImmediatePostDominator(cur);
        }
    }

    public static PostDominatorTree Compute(ControlFlowGraph cfg)
    {
        var exitPreds = cfg.Blocks
            .Where(b => cfg.Successors(b.Id).IsEmpty)
            .Select(b => b.Id)
            .ToArray();

        IReadOnlyList<int> Succs(int b) => b == VirtualExitId
            ? exitPreds
            : cfg.Predecessors(b).Select(e => e.FromBlock).Distinct().ToArray();

        IReadOnlyList<int> Preds(int b)
        {
            var real = cfg.Successors(b).Select(e => e.ToBlock).Distinct().ToArray();
            return real.Length == 0 ? [VirtualExitId] : real;
        }

        var idom = DominatorAlgorithm.ComputeIdom(VirtualExitId, Preds, Succs);
        return new PostDominatorTree(idom);
    }
}
