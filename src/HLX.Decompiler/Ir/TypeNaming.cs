namespace HLX.Decompiler;

public static class TypeNaming
{
    public static string ShortTypeName(HlType type) => type switch
    {
        PrimitiveType p => p.Kind.ToString().ToLowerInvariant(),
        ObjectType o    => o.Name.Contains('.') ? o.Name[(o.Name.LastIndexOf('.') + 1)..] : o.Name,
        AbstractType a  => a.Name,
        EnumType e      => e.Name.Contains('.') ? e.Name[(e.Name.LastIndexOf('.') + 1)..] : e.Name,
        VirtualType     => "Virtual",
        FunctionType    => "Function",
        ReferenceType   => "Ref",
        _               => "?"
    };
}
