namespace HLX.App.ViewModels.Detail;

public sealed class ProtoItem
{
    public string Name { get; }
    public int FunctionFIndex { get; }
    public string Signature { get; }
    public IRelayCommand NavigateCommand { get; }

    public ProtoItem(HlProto proto, HlModule module, TypeNameResolver resolver, Action<int> navigate)
    {
        Name = proto.Name;
        FunctionFIndex = proto.FunctionIndex;
        Signature = BuildSignature(proto.FunctionIndex, module, resolver);
        NavigateCommand = new RelayCommand(() => navigate(proto.FunctionIndex));
    }

    private static string BuildSignature(int findex, HlModule module, TypeNameResolver resolver)
    {
        FunctionType? ft = null;
        if (findex < module.Natives.Length)
            ft = module.Natives.FirstOrDefault(n => n.FunctionIndex == findex)?.Type;
        else
        {
            int idx = findex - module.Natives.Length;
            if ((uint)idx < (uint)module.Functions.Length)
                ft = module.Functions[idx].Type;
        }
        if (ft == null) return "?";
        var args = string.Join(", ", ft.ArgTypes.Select(resolver.Resolve));
        return $"({args}) -> {resolver.Resolve(ft.ReturnType)}";
    }
}
