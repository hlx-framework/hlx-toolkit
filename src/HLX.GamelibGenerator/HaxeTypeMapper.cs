using HLX.Core;

namespace HLX.GamelibGenerator;

/// <summary>
/// HL kind -&gt; Haxe type-name mapping. Every <c>Dynamic</c> fallback records a
/// human-readable reason, surfaced as a trailing comment in generated source, so
/// "erased on purpose" is distinguishable from "the generator gave up".
/// </summary>
internal sealed class HaxeTypeMapper(
    HlModule module,
    IReadOnlySet<string> includedClassNames,
    IReadOnlySet<string> includedEnumNames)
{
    // Package-qualified secondary-type name -> real module-qualified path (see KnownModuleQualifiedPaths).
    private static readonly IReadOnlyDictionary<string, string> knownModuleQualifiedPaths =
        KnownModuleQualifiedPaths.Table;

    public MappedType Map(int typeIndex)
    {
        if ((uint)typeIndex >= (uint)module.Types.Length)
            return new MappedType("Dynamic", $"type index {typeIndex} out of range");
        return Map(module.Types[typeIndex]);
    }

    public MappedType Map(HlType t) => t switch
    {
        PrimitiveType p => MapPrimitive(p.Kind),
        ObjectType o => MapObjectType(o),
        EnumType e => MapEnumType(e),
        AbstractType a => MapNamedFallback(a.Name, "abstract"),
        VirtualType => new MappedType("Dynamic", "HL 'virtual' structural type has no fixed Haxe shape"),
        FunctionType f => MapFunctionType(f),
        ReferenceType r => MapReferenceType(r),
        _ => new MappedType("Dynamic", $"unrecognized HL type kind {t.GetType().Name}")
    };

    // HL makes the receiver an explicit first argument for instance methods; drop it
    // from the Haxe-visible parameter list (the call site re-adds `this` explicitly).
    // Static-bound functions have no receiver, so dropSelf is false for those.
    public (IReadOnlyList<MappedType> Params, MappedType Return) MapCallable(FunctionType f, bool dropSelf)
    {
        var argIdxs = dropSelf && f.ArgTypes.Length > 0 ? f.ArgTypes.Skip(1) : f.ArgTypes;
        var ps = argIdxs.Select(Map).ToList();
        var ret = Map(f.ReturnType);
        return (ps, ret);
    }

    private static MappedType MapPrimitive(PrimitiveKind k) => k switch
    {
        PrimitiveKind.Void => new MappedType("Void", null),
        PrimitiveKind.U8 => new MappedType("hl.UI8", null),
        PrimitiveKind.U16 => new MappedType("hl.UI16", null),
        PrimitiveKind.I32 => new MappedType("Int", null),
        PrimitiveKind.I64 => new MappedType("haxe.Int64", null),
        PrimitiveKind.F32 => new MappedType("hl.F32", null),
        PrimitiveKind.F64 => new MappedType("Float", null),
        PrimitiveKind.Bool => new MappedType("Bool", null),
        PrimitiveKind.Bytes => new MappedType("hl.Bytes", null),
        PrimitiveKind.Dyn => new MappedType("Dynamic", null),
        PrimitiveKind.Array => new MappedType("Dynamic", "raw HL 'array' kind carries no element-type info at this level"),
        PrimitiveKind.Type => new MappedType("hl.BaseType", null),
        PrimitiveKind.DynObj => new MappedType("Dynamic", "HL 'dynobj' (anonymous structure) has no fixed Haxe shape"),
        PrimitiveKind.Guid => new MappedType("Dynamic", "HL 'guid' kind has no standard Haxe equivalent"),
        _ => new MappedType("Dynamic", $"unrecognized primitive kind {k}")
    };

    // Real Haxe generic classes whose type parameter(s) can't be recovered from
    // bytecode (no generic metadata) - erased to Dynamic args so the reference still
    // parses as a complete type instead of a bare, arity-mismatched name.
    private static readonly Dictionary<string, int> KnownGenericArity = new()
    {
        ["haxe.ds.StringMap"] = 1,
        ["haxe.ds.IntMap"] = 1,
        ["haxe.ds.ObjectMap"] = 2,
        ["haxe.ds.EnumValueMap"] = 2,
        ["haxe.ds.WeakMap"] = 2,
        ["haxe.ds.Vector"] = 1,
        ["haxe.ds.List"] = 1,
        ["haxe.iterators.ArrayIterator"] = 1,
        ["hl.types.IntMap"] = 1,
        ["hl.types.BytesMap"] = 1,
        ["hl.types.ObjectMap"] = 2,
        ["hl.types.Int64Map"] = 1,
        ["domkit.Properties"] = 1,
        ["domkit.PropertyHandler"] = 2,
        ["domkit.Component"] = 2,
    };

    private MappedType MapObjectType(ObjectType o)
    {
        var name = o.Name;

        // hl.types.* specializations backing an ordinary Haxe Array<T>.
        if (name is "hl.types.ArrayObj" or "hl.types.ArrayDyn")
            return new MappedType("Array<Dynamic>", null);
        if (name.StartsWith("hl.types.ArrayBytes_", StringComparison.Ordinal))
        {
            var suffix = name["hl.types.ArrayBytes_".Length..];
            var elem = suffix.Contains('_') ? suffix.Replace('_', '.') : suffix;
            return new MappedType($"Array<{elem}>", null);
        }
        if (knownModuleQualifiedPaths.TryGetValue(name, out var qualifiedPath))
            return new MappedType(qualifiedPath, null);

        // A locally-generated wrapper is always non-generic, even if the real upstream
        // class also appears in KnownGenericArity (e.g. domkit.Properties) - must be
        // checked before that table.
        if (includedClassNames.Contains(name) && Naming.LooksLikeValidHaxeTypePath(name))
            return new MappedType(name, null);

        if (KnownGenericArity.TryGetValue(name, out var arity))
        {
            var args = string.Join(", ", Enumerable.Repeat("Dynamic", arity));
            return new MappedType($"{name}<{args}>", null);
        }

        if (Naming.HasUnreferenceableSegment(name))
            return new MappedType("Dynamic", $"'{name}' is a companion/private/nested type, not directly referenceable");

        if (Naming.IsExcludedNamespace(name))
        {
            return Naming.LooksLikeValidHaxeTypePath(name)
                ? new MappedType(name, null)
                : new MappedType("Dynamic", $"'{name}' is excluded from generation and not a valid Haxe type path");
        }

        return new MappedType("Dynamic", $"'{name}' not generated (excluded, invalid identifier, or filtered out)");
    }

    private MappedType MapEnumType(EnumType e)
    {
        var name = e.Name;
        if (knownModuleQualifiedPaths.TryGetValue(name, out var qualifiedPath))
            return new MappedType(qualifiedPath, null);
        if (Naming.HasUnreferenceableSegment(name))
            return new MappedType("Dynamic", $"'{name}' is a companion/private/nested enum type");
        if (Naming.IsExcludedNamespace(name))
        {
            return Naming.LooksLikeValidHaxeTypePath(name)
                ? new MappedType(name, null)
                : new MappedType("Dynamic", $"'{name}' is excluded and not a valid Haxe type path");
        }
        if (includedEnumNames.Contains(name) && Naming.LooksLikeValidHaxeTypePath(name))
            return new MappedType(name, null);
        return new MappedType("Dynamic", $"'{name}' (enum) not generated (excluded, invalid identifier, or filtered out)");
    }

    private static MappedType MapNamedFallback(string name, string kindLabel)
    {
        if (Naming.HasUnreferenceableSegment(name))
            return new MappedType("Dynamic", $"'{name}' is a private/nested {kindLabel} type");
        if (Naming.IsThirdPartyNativeAbiName(name))
            return new MappedType("Dynamic", $"'{name}' is a third-party native ABI type (e.g. FMOD) with no Haxe declaration this generator's output can rely on");
        return Naming.LooksLikeValidHaxeTypePath(name)
            ? new MappedType(name, null)
            : new MappedType("Dynamic", $"'{name}' ({kindLabel}) is a native/internal name, not a directly referenceable Haxe type");
    }

    private MappedType MapFunctionType(FunctionType f)
    {
        var args = new List<string>();
        string? reason = null;
        foreach (var argIdx in f.ArgTypes)
        {
            var m = Map(argIdx);
            args.Add(m.HaxeType);
            reason ??= m.FallbackReason;
        }
        var ret = Map(f.ReturnType);
        reason ??= ret.FallbackReason;

        // Haxe's `->` is right-associative: a function-typed return value needs an
        // extra pair of parens or the parser flattens it into the outer arg list.
        var retTypeAtIndex = (uint)f.ReturnType < (uint)module.Types.Length ? module.Types[f.ReturnType] : null;
        var retStr = retTypeAtIndex is FunctionType ? $"({ret.HaxeType})" : ret.HaxeType;

        return new MappedType($"({string.Join(", ", args)}) -> {retStr}", reason);
    }

    private MappedType MapReferenceType(ReferenceType r)
    {
        var inner = Map(r.InnerTypeIndex);
        return r.Kind switch
        {
            // Null<T> is an exact, real Haxe equivalent.
            ReferenceKind.Null => new MappedType($"Null<{inner.HaxeType}>", inner.FallbackReason),
            // hl.Ref<T> confirmed present in the real Haxe HL stdlib (std/hl/Ref.hx).
            ReferenceKind.Ref => new MappedType($"hl.Ref<{inner.HaxeType}>", inner.FallbackReason),
            // packed<T> (struct-packing) has no ordinary Haxe surface; approximated as
            // plain T, which is honest for read purposes even if not 100% precise.
            ReferenceKind.Packed => new MappedType(inner.HaxeType, Combine(inner.FallbackReason, "packed<T> approximated as plain T")),
            _ => new MappedType("Dynamic", "unrecognized reference kind")
        };
    }

    private static string Combine(string? a, string b) => a == null ? b : $"{a}; {b}";
}
