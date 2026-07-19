namespace HLX.Analysis;

// Findices are the unified space: natives [0..N-1], functions [N..N+F-1].
public sealed class CallGraph
{
    private readonly ImmutableDictionary<int, ImmutableArray<int>> _callees;
    private readonly ImmutableDictionary<int, ImmutableArray<int>> _callers;

    public CallGraph(HlModule module)
    {
        var calleesBuilder = new Dictionary<int, HashSet<int>>();
        var callersBuilder = new Dictionary<int, HashSet<int>>();

        foreach (var fn in module.Functions)
        {
            foreach (var instr in fn.Instructions)
            {
                var kinds = HlOpcodeInfo.Operands(instr.Opcode);
                bool stop = false;
                for (int i = 0; i < kinds.Length && i < instr.Operands.Length && !stop; i++)
                {
                    switch (kinds[i])
                    {
                        case HlOperandKind.CallArgs:
                        case HlOperandKind.SwitchTable:
                            stop = true;
                            break;
                        case HlOperandKind.FunctionRef:
                            int callee = instr.Operands[i];
                            int caller = fn.FunctionIndex;

                            if (!calleesBuilder.TryGetValue(caller, out var calleeSet))
                                calleesBuilder[caller] = calleeSet = [];
                            calleeSet.Add(callee);

                            if (!callersBuilder.TryGetValue(callee, out var callerSet))
                                callersBuilder[callee] = callerSet = [];
                            callerSet.Add(caller);
                            break;
                    }
                }
            }
        }

        _callees = calleesBuilder.ToImmutableDictionary(
            kv => kv.Key, kv => kv.Value.OrderBy(x => x).ToImmutableArray());
        _callers = callersBuilder.ToImmutableDictionary(
            kv => kv.Key, kv => kv.Value.OrderBy(x => x).ToImmutableArray());
    }

    public ImmutableArray<int> Callees(int functionFIndex) =>
        _callees.TryGetValue(functionFIndex, out var arr) ? arr : ImmutableArray<int>.Empty;

    public ImmutableArray<int> Callers(int functionFIndex) =>
        _callers.TryGetValue(functionFIndex, out var arr) ? arr : ImmutableArray<int>.Empty;

    public bool IsNonEmpty => _callees.Count > 0;
}
