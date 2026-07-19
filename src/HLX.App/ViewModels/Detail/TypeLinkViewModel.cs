namespace HLX.App.ViewModels.Detail;

public sealed class TypeLinkViewModel : InstructionPartViewModel
{
    public override string Text { get; }
    public override bool IsLink => true;
    public int TypeIndex { get; }
    public IRelayCommand NavigateCommand { get; }

    public TypeLinkViewModel(string text, int typeIndex, Action<int> navigate)
    {
        Text = text;
        TypeIndex = typeIndex;
        NavigateCommand = new RelayCommand(() => navigate(typeIndex));
    }
}
