namespace HLX.App.ViewModels;

public sealed class SearchResultViewModel
{
    public string Label { get; }
    public TreeNodeViewModel Node { get; }
    public IRelayCommand NavigateCommand { get; }

    public SearchResultViewModel(string label, TreeNodeViewModel node, Action<TreeNodeViewModel> navigate)
    {
        Label = label;
        Node = node;
        NavigateCommand = new RelayCommand(() => navigate(node));
    }
}

