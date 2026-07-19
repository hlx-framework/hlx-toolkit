namespace HLX.Analysis;

// Indexes instruction-level TypeIndex operands only, not register declarations.
public sealed class ReferenceIndex
{
    private readonly ImmutableDictionary<int, ImmutableArray<int>> _typeToFunctions;

    public ReferenceIndex(HlModule module)
    {
        var builder = new Dictionary<int, HashSet<int>>();

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
                        case HlOperandKind.TypeIndex:
                            int typeIdx = instr.Operands[i];
                            if (!builder.TryGetValue(typeIdx, out var set))
                                builder[typeIdx] = set = [];
                            set.Add(fn.FunctionIndex);
                            break;
                    }
                }
            }
        }

        _typeToFunctions = builder.ToImmutableDictionary(
            kv => kv.Key,
            kv => kv.Value.OrderBy(x => x).ToImmutableArray());
    }

    public ImmutableArray<int> FunctionsReferencingType(int typeIndex) =>
        _typeToFunctions.TryGetValue(typeIndex, out var arr) ? arr : ImmutableArray<int>.Empty;

    public int IndexedTypeCount => _typeToFunctions.Count;
}
