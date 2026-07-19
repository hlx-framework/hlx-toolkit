namespace HLX.App.ViewModels;

public sealed class GlobalNodeViewModel : TreeNodeViewModel
{
    public int Slot { get; }
    public int TypeIndex { get; }

    private readonly string _header;
    public override string Header => _header;

    public GlobalNodeViewModel(int slot, int typeIndex, string typeName)
    {
        Slot = slot;
        TypeIndex = typeIndex;
        _header = $"global#{slot}: {typeName}";
    }
}
