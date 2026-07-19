namespace HLX.Core;

public sealed record HlModule(
    HlHeader Header,
    ImmutableArray<int> Ints,
    ImmutableArray<double> Floats,
    ImmutableArray<string> Strings,
    ImmutableArray<byte[]> Bytes,        // populated only when v5+ bytes pool is present
    ImmutableArray<HlType> Types,
    ImmutableArray<HlNative> Natives,
    ImmutableArray<HlFunction> Functions,
    ImmutableArray<int> Globals,         // type indices for each global slot
    ImmutableArray<string> DebugFiles,   // source file table (empty when no debug info)
    int EntryPoint                       // index into Functions
);
