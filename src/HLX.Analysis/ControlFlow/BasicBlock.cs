namespace HLX.Analysis;

// Start/End are half-open instruction offsets: [Start, End).
public sealed record BasicBlock(
    int Id,
    int Start,
    int End,
    ImmutableArray<HlInstruction> Instructions
);
