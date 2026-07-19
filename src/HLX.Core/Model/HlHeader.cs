namespace HLX.Core;

[Flags]
public enum HlFeatureFlags
{
    None        = 0,
    HasDebugInfo = 1 << 0,
}

public sealed record HlHeader(int Version, HlFeatureFlags Flags);
