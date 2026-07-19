namespace HLX.Core;

// Jump operands are relative to Offset; Analysis resolves them to target indices.
public sealed record HlInstruction(
    HlOpcode Opcode,
    ImmutableArray<int> Operands,
    int Offset
);
