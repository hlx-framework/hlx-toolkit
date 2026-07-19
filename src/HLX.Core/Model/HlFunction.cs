namespace HLX.Core;

public sealed record HlDebugInfo(int FileIndex, int Line);

public sealed record HlNative(
    string Lib,
    string Name,
    FunctionType Type,
    int FunctionIndex
);

public sealed record HlFunction(
    FunctionType Type,
    int FunctionIndex,
    ImmutableArray<HlType> Registers,
    ImmutableArray<HlInstruction> Instructions,
    ImmutableArray<HlDebugInfo> DebugInfo    // empty when HasDebugInfo is not set
);
