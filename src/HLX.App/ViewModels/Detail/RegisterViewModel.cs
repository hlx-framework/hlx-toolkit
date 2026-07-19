namespace HLX.App.ViewModels.Detail;

public sealed class RegisterViewModel
{
    public string Index { get; }
    public string TypeName { get; }

    public RegisterViewModel(int index, HlType type, TypeNameResolver resolver, HlModule module)
    {
        Index = $"r{index}";
        TypeName = FormatRegType(type, resolver, module);
    }

    private static string FormatRegType(HlType type, TypeNameResolver resolver, HlModule module)
    {
        for (int i = 0; i < module.Types.Length; i++)
            if (module.Types[i] == type)
                return resolver.Resolve(i);
        return type switch
        {
            PrimitiveType p => p.Kind.ToString().ToLowerInvariant(),
            ObjectType o => o.Name,
            AbstractType a => a.Name,
            EnumType e => e.Name,
            VirtualType => "virtual",
            FunctionType => "fun",
            ReferenceType r => r.Kind.ToString().ToLowerInvariant(),
            _ => "?"
        };
    }
}
