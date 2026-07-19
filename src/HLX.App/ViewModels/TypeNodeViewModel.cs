namespace HLX.App.ViewModels;

public sealed class TypeNodeViewModel : TreeNodeViewModel
{
    public int TypeIndex { get; }
    public HlType Type { get; }

    private readonly string _name;
    private readonly IReadOnlyList<TreeNodeViewModel>? _children;

    public override string Header => _name;
    public override IReadOnlyList<TreeNodeViewModel>? Children => _children;
    public override IRelayCommand? FindUsagesCommand { get; }
    public override bool CanFindUsages => true;

    public TypeNodeViewModel(int typeIndex, HlType type, string name, Action<TypeNodeViewModel> onFindUsages,
        IReadOnlyList<TreeNodeViewModel>? children = null)
    {
        TypeIndex = typeIndex;
        Type = type;
        _name = name;
        _children = children is { Count: > 0 } ? children : null;
        FindUsagesCommand = new RelayCommand(() => onFindUsages(this));
    }
}
