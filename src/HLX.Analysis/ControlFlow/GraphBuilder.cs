namespace HLX.Analysis;

public static class GraphBuilder
{
    public static ControlFlowGraph Build(HlFunction function)
    {
        if (function.Instructions.IsEmpty)
            return new ControlFlowGraph(function, [], [], 0);

        int count = function.Instructions.Length;

        // Leaders: offset 0, jump/switch targets, and the instruction after any
        // instruction with a successor edge (edge generation below only looks
        // at each block's last instruction).
        var leaders = new SortedSet<int> { 0 };
        foreach (var instr in function.Instructions)
        {
            var shape = ControlFlowClassifier.Classify(instr);
            foreach (var (target, _, _) in shape.Successors)
                if ((uint)target < (uint)count) leaders.Add(target);
            bool endsBlock = !shape.FallsThrough || shape.Successors.Length > 0;
            if (endsBlock && instr.Offset + 1 < count)
                leaders.Add(instr.Offset + 1);
        }

        var leaderList = leaders.ToImmutableArray();
        var blocksBuilder = ImmutableArray.CreateBuilder<BasicBlock>(leaderList.Length);
        for (int i = 0; i < leaderList.Length; i++)
        {
            int start = leaderList[i];
            int end = i + 1 < leaderList.Length ? leaderList[i + 1] : count;
            blocksBuilder.Add(new BasicBlock(i, start, end, Slice(function.Instructions, start, end)));
        }
        var blocks = blocksBuilder.ToImmutable();

        var blockByOffset = new Dictionary<int, int>();
        foreach (var b in blocks)
            for (int o = b.Start; o < b.End; o++)
                blockByOffset[o] = b.Id;

        var edges = ImmutableArray.CreateBuilder<CfgEdge>();
        foreach (var block in blocks)
        {
            var last = block.Instructions[^1];
            var shape = ControlFlowClassifier.Classify(last);

            foreach (var (target, kind, caseValue) in shape.Successors)
                edges.Add(new CfgEdge(block.Id, blockByOffset[target], kind, caseValue));

            if (shape.FallsThrough && block.End < count)
                edges.Add(new CfgEdge(block.Id, blockByOffset[block.End], EdgeKind.Fallthrough));
        }

        int entryId = blockByOffset[0];
        return new ControlFlowGraph(function, blocks, edges.ToImmutable(), entryId);
    }

    private static ImmutableArray<HlInstruction> Slice(ImmutableArray<HlInstruction> all, int start, int end)
    {
        var builder = ImmutableArray.CreateBuilder<HlInstruction>(end - start);
        for (int i = start; i < end; i++) builder.Add(all[i]);
        return builder.MoveToImmutable();
    }
}
