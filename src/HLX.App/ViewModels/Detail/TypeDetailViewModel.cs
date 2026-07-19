namespace HLX.App.ViewModels.Detail;

public sealed class TypeDetailViewModel
{
    public int TypeIndex { get; }
    public string TypeName { get; }
    public string KindLabel { get; }

    public TypeLinkItem? SuperType { get; }
    public IReadOnlyList<TypeLinkItem> Fields { get; } = [];
    public IReadOnlyList<ProtoItem> Protos { get; } = [];
    public IReadOnlyList<BindingItem> Bindings { get; } = [];

    public IReadOnlyList<EnumConstructItem> Constructs { get; } = [];

    public IReadOnlyList<TypeLinkItem> VirtualFields { get; } = [];

    public TypeLinkItem? InnerType { get; }
    public string? ReferenceKind { get; }

    public IReadOnlyList<TypeLinkItem> ArgTypes { get; } = [];
    public TypeLinkItem? ReturnType { get; }
    public bool IsMethod { get; }

    public TypeDetailViewModel(
        int typeIndex,
        HlType type,
        HlModule module,
        AnalysisResult analysis,
        INavigationService nav)
    {
        TypeIndex = typeIndex;
        var resolver = analysis.TypeNames;
        TypeName = resolver.Resolve(typeIndex);

        Action<int> navType = nav.NavigateToType;
        Action<int> navFunc = nav.NavigateToFunction;

        var funcNames = new Dictionary<int, string>();
        foreach (var t in module.Types.OfType<ObjectType>())
            foreach (var p in t.Protos)
                funcNames.TryAdd(p.FunctionIndex, $"{t.Name}::{p.Name}");

        switch (type)
        {
            case ObjectType obj:
                KindLabel = obj.IsStruct ? "Struct" : "Object";
                SuperType = obj.SuperIndex.HasValue
                    ? new TypeLinkItem("", obj.SuperIndex.Value, resolver, navType) : null;
                Fields = obj.Fields.Select(f => new TypeLinkItem(f.Name, f.TypeIndex, resolver, navType)).ToList();
                Protos = obj.Protos.Select(p => new ProtoItem(p, module, resolver, navFunc)).ToList();
                Bindings = obj.Bindings.Select(b => new BindingItem(b, obj, module, funcNames, navFunc)).ToList();
                break;

            case EnumType en:
                KindLabel = "Enum";
                Constructs = en.Constructs.Select(c => new EnumConstructItem(c, resolver, navType)).ToList();
                break;

            case VirtualType vt:
                KindLabel = "Virtual";
                VirtualFields = vt.Fields.Select(f => new TypeLinkItem(f.Name, f.TypeIndex, resolver, navType)).ToList();
                break;

            case AbstractType:
                KindLabel = "Abstract";
                break;

            case PrimitiveType p:
                KindLabel = $"Primitive ({p.Kind})";
                break;

            case FunctionType ft:
                KindLabel = ft.IsMethod ? "Method" : "Function";
                IsMethod = ft.IsMethod;
                ArgTypes = ft.ArgTypes.Select(ti => new TypeLinkItem("", ti, resolver, navType)).ToList();
                ReturnType = new TypeLinkItem("", ft.ReturnType, resolver, navType);
                break;

            case ReferenceType rt:
                KindLabel = "Reference";
                ReferenceKind = rt.Kind.ToString();
                InnerType = new TypeLinkItem("", rt.InnerTypeIndex, resolver, navType);
                break;

            default:
                KindLabel = type.GetType().Name;
                break;
        }
    }
}
