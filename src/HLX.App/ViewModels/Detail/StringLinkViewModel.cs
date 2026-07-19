namespace HLX.App.ViewModels.Detail;

public sealed class StringLinkViewModel : InstructionPartViewModel
{
    public override string Text { get; }
    public override bool IsLink => true;
    public string FullString { get; }
    public IRelayCommand NavigateCommand { get; }

    public StringLinkViewModel(string display, string fullStr, Action<string> showString)
    {
        Text = display;
        FullString = fullStr;
        NavigateCommand = new RelayCommand(() => showString(fullStr));
    }
}
