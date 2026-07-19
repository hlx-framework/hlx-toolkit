namespace HLX.App.ViewModels.Detail;

public sealed class TypeLinkItem
{
    public string Name { get; }
    public string TypeName { get; }
    public int TypeIndex { get; }
    public IRelayCommand NavigateCommand { get; }

    public TypeLinkItem(string name, int typeIndex, TypeNameResolver resolver, Action<int> navigate)
    {
        Name = name;
        TypeIndex = typeIndex;
        TypeName = resolver.Resolve(typeIndex);
        NavigateCommand = new RelayCommand(() => navigate(typeIndex));
    }
}
