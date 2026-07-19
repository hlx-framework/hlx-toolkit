namespace HLX.Analysis;

public sealed class TypeNameResolver
{
    private readonly ImmutableArray<HlType> _types;

    public TypeNameResolver(HlModule module) => _types = module.Types;

    public string Resolve(int typeIndex) => Resolve(typeIndex, depth: 0);

    private string Resolve(int typeIndex, int depth)
    {
        if ((uint)typeIndex >= (uint)_types.Length)
            return $"?{typeIndex}";
        return Format(_types[typeIndex], typeIndex, depth);
    }

    private string Format(HlType type, int index, int depth) => type switch
    {
        PrimitiveType p => p.Kind.ToString().ToLowerInvariant(),
        ObjectType o    => o.Name,
        AbstractType a  => a.Name,
        EnumType e      => e.Name,
        VirtualType     => $"virtual#{index}",
        FunctionType f  => depth < 4 ? FormatFunction(f, depth) : "fun(...)",
        ReferenceType r => depth < 4 ? FormatRef(r, depth) : r.Kind.ToString().ToLowerInvariant(),
        _               => $"type#{index}",
    };

    private string FormatFunction(FunctionType f, int depth)
    {
        var args = f.ArgTypes.Select(i => Resolve(i, depth + 1));
        var ret = Resolve(f.ReturnType, depth + 1);
        return $"{(f.IsMethod ? "method" : "fun")}({string.Join(", ", args)}) -> {ret}";
    }

    private string FormatRef(ReferenceType r, int depth) =>
        $"{r.Kind.ToString().ToLowerInvariant()}<{Resolve(r.InnerTypeIndex, depth + 1)}>";
}
