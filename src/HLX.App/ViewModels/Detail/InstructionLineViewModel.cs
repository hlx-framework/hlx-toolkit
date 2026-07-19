namespace HLX.App.ViewModels.Detail;

public sealed class InstructionLineViewModel
{
    public string? Label { get; }
    public string Offset { get; }
    public string Mnemonic { get; }
    public IReadOnlyList<InstructionPartViewModel> Parts { get; }
    public string? DebugInfo { get; }

    public InstructionLineViewModel(
        string? label,
        string offset,
        string mnemonic,
        IReadOnlyList<InstructionPartViewModel> parts,
        string? debugInfo)
    {
        Label = label;
        Offset = offset;
        Mnemonic = mnemonic;
        Parts = parts;
        DebugInfo = debugInfo;
    }

    public bool HasLabel => Label != null;
    public bool HasDebugInfo => DebugInfo != null;
}
