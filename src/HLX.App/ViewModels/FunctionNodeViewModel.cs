namespace HLX.App.ViewModels;

public sealed class FunctionNodeViewModel : TreeNodeViewModel
{
    public int FunctionFIndex { get; }
    public bool IsNative { get; }

    private readonly string _name;
    public override string Header => _name;
    public override IRelayCommand? FindUsagesCommand { get; }
    public override bool CanFindUsages => true;

    public FunctionNodeViewModel(int findex, string name, bool isNative, Action<FunctionNodeViewModel> onFindUsages)
    {
        FunctionFIndex = findex;
        IsNative = isNative;
        _name = name;
        FindUsagesCommand = new RelayCommand(() => onFindUsages(this));
    }
}
