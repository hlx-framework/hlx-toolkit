namespace HLX.App.ViewModels.Detail;

public sealed class EnumConstructItem
{
    public string Name { get; }
    public IReadOnlyList<TypeLinkItem> ParamTypes { get; }

    public EnumConstructItem(HlEnumConstruct construct, TypeNameResolver resolver, Action<int> navigate)
    {
        Name = construct.Name;
        ParamTypes = construct.ParamTypes
            .Select(ti => new TypeLinkItem("", ti, resolver, navigate))
            .ToList();
    }
}
