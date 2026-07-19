namespace HLX.App.ViewModels.Detail;

public sealed class CallerItem
{
    public string Name { get; }
    public int FunctionFIndex { get; }
    public IRelayCommand NavigateCommand { get; }

    public CallerItem(string name, int findex, Action<int> navigate)
    {
        Name = name;
        FunctionFIndex = findex;
        NavigateCommand = new RelayCommand(() => navigate(findex));
    }
}
