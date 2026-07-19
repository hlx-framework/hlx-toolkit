namespace HLX.App.ViewModels.Detail;

public enum PseudoLineKind { Label, Code, Comment }

public sealed record PseudoCodeLine(string Text, PseudoLineKind Kind)
{
    public string Foreground => Kind switch
    {
        PseudoLineKind.Label   => "#C586C0",
        PseudoLineKind.Comment => "#6A9955",
        _                      => "#D4D4D4"
    };
}
