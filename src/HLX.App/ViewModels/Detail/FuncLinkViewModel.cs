namespace HLX.App.ViewModels.Detail;

public sealed class FuncLinkViewModel : InstructionPartViewModel
{
    public override string Text { get; }
    public override bool IsLink => true;
    public int FunctionFIndex { get; }
    public IRelayCommand NavigateCommand { get; }

    public FuncLinkViewModel(string text, int fIndex, Action<int> navigate)
    {
        Text = text;
        FunctionFIndex = fIndex;
        NavigateCommand = new RelayCommand(() => navigate(fIndex));
    }
}
