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

    // A std name MapObjectType/MapEnumType always resolves via an earlier special case -
    // a real Array<T> backing type, or a module-qualified secondary-type rename (see
    // KnownModuleQualifiedPaths) - before ever reaching the wrap-vs-direct-reference
    // check. Building a wrapper for one of these would be dead output: nothing will
    // ever reference it under this identity. ClassCollector/EnumCollector check this
    // before routing a std name through StdWrapperClassifier at all, so it's never
    // added to StdWrapperCandidateNames in the first place.
    public static bool IsAlwaysResolvedBeforeWrapCheck(string name) =>
        name is "hl.types.ArrayObj" or "hl.types.ArrayDyn"
        || name.StartsWith("hl.types.ArrayBytes_", StringComparison.Ordinal)
        || KnownModuleQualifiedPaths.Table.ContainsKey(name);

    // A cyclic/self-referential virtual (or function/reference) type chain is guarded
    // two ways: `visiting` (threaded through every recursive descent below) tracks type
    // indices currently being expanded on the CURRENT path - added in MapIndexed before
    // recursing, removed again once that branch finishes, so a diamond-shaped
    // non-cyclic revisit from a sibling branch is still fine, but a real cycle back to
    // an ancestor on the path is caught immediately. MaxNestingDepth is a backstop for
    // pathological (non-cyclic) deep nesting that the visited-set alone wouldn't catch.
    private const int MaxNestingDepth = 64;

    public MappedType Map(int typeIndex) => MapIndexed(typeIndex, [], 0);

    public MappedType Map(HlType t) => MapCore(t, [], 0);

    private MappedType MapIndexed(int typeIndex, HashSet<int> visiting, int depth)
    {
        if ((uint)typeIndex >= (uint)module.Types.Length)
            return new MappedType("Dynamic", $"type index {typeIndex} out of range");
        if (depth > MaxNestingDepth)
            return new MappedType("Dynamic", "type nesting exceeded max recursion depth");
        if (!visiting.Add(typeIndex))
            return new MappedType("Dynamic", $"cyclic type reference back to type index {typeIndex}");
        try
        {
            return MapCore(module.Types[typeIndex], visiting, depth + 1);
        }
        finally
        {
            visiting.Remove(typeIndex);
        }
    }

    private MappedType MapCore(HlType t, HashSet<int> visiting, int depth) => t switch
    {
        PrimitiveType p => MapPrimitive(p.Kind),
        ObjectType o => MapObjectType(o),
        EnumType e => MapEnumType(e),
        AbstractType a => MapNamedFallback(a.Name, "abstract"),
        VirtualType v => MapVirtualType(v, visiting, depth),
        FunctionType f => MapFunctionType(f, visiting, depth),
        ReferenceType r => MapReferenceType(r, visiting, depth),
        _ => new MappedType("Dynamic", $"unrecognized HL type kind {t.GetType().Name}")
    };

    // HL's "virtual" kind is a fixed, named field list declared at compile time - real
    // structure, unlike the genuinely shapeless "dynobj" kind (see PrimitiveKind.DynObj,
    // untouched). In real Haxe source, Iterator<T>/Iterable<T>/KeyValueIterator<K,V> ARE
    // just anonymous-structure typedefs, so emitting a plain anon struct here (rather
    // than Dynamic) lets Haxe's own structural typing unify a wrapper's iterator()/
    // keys()/keyValueIterator() etc. against those interfaces - which is exactly what a
    // `for` loop over a wrapper's iterator() requires. This is deliberately general:
    // it fixes every structural shape (game-class virtual params/returns too), not just
    // recognized iterator shapes.
    //
    // A field name that isn't a legal plain Haxe identifier (empty, a reserved word, ...)
    // can't be safely emitted - and dropping just that field would produce a silently
    // PARTIAL structure (e.g. an Iterator missing `next`), worse than an honest Dynamic.
    // So an unnameable field takes down the whole virtual type, not just itself.
    private MappedType MapVirtualType(VirtualType v, HashSet<int> visiting, int depth)
    {
        if (v.Fields.Length == 0)
            return new MappedType("{}", null);

        var fieldStrs = new List<string>();
        string? reason = null;
        foreach (var f in v.Fields)
        {
            if (!Naming.IsValidPlainIdentifier(f.Name))
                return new MappedType("Dynamic",
                    $"HL 'virtual' structural type has a field ('{f.Name}') that isn't a usable plain Haxe identifier; whole type erased rather than emitting a partial shape");

            var fieldType = MapIndexed(f.TypeIndex, visiting, depth);
            reason ??= fieldType.FallbackReason;
            fieldStrs.Add($"{f.Name}:{fieldType.HaxeType}");
        }
        return new MappedType($"{{ {string.Join(", ", fieldStrs)} }}", reason);
    }

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
    // parses as a complete type instead of a bare, arity-mismatched name. Reached for a
    // class referenced DIRECTLY: domkit.* (already-safe locally-generated wrappers), and
    // - real compile failure found live ("Not enough type parameters for haxe.ds.TreeNode",
    // hit via haxe.ds.BalancedTree.root once BalancedTree itself started getting a real
    // generated wrapper) - a knownModuleQualifiedPaths secondary-type rename (see below,
    // and MapObjectType's own qualifiedPath branch) that happens to also be generic. Most
    // std rows below are otherwise unreachable through the ordinary wrap-vs-direct-reference
    // std-namespace branch (that always returns first for anything NOT intercepted by
    // knownModuleQualifiedPaths) - harmless, kept for the rare qualifiedPath-generic case above.
    internal static readonly Dictionary<string, int> KnownGenericArity = new()
    {
        ["haxe.ds.StringMap"] = 1,
        ["haxe.ds.IntMap"] = 1,
        ["haxe.ds.ObjectMap"] = 2,
        ["haxe.ds.EnumValueMap"] = 2,
        ["haxe.ds.WeakMap"] = 2,
        ["haxe.ds.Vector"] = 1,
        ["haxe.ds.List"] = 1,
        ["haxe.ds.TreeNode"] = 2,
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
            return new MappedType(ApplyKnownGenericArity(name, qualifiedPath), null);

        // In scope for generation - either an ordinary game class (includedClassNames is
        // ClassCollector.CandidateNames) or a std wrapper candidate
        // (ClassCollector.StdWrapperCandidateNames, unioned in by the caller - see
        // Program.cs). A std wrapper's own generated file lives under "hlx.std.<name>",
        // never under its own real name - referencing it AS ITSELF is exactly the
        // cross-module SafeCast bug this whole indirection exists to dodge (an ordinary
        // Haxe class/enum recompiled fresh by every mod's own `haxe` invocation).
        if (includedClassNames.Contains(name) && Naming.LooksLikeValidHaxeTypePath(name))
        {
            var wrapperPath = Naming.IsStdNamespace(name) ? Naming.StdWrapperPackagePrefix + name : name;
            return new MappedType(wrapperPath, null);
        }

        if (Naming.HasUnreferenceableSegment(name))
            return new MappedType("Dynamic", $"'{name}' is a companion/private/nested type, not directly referenceable");

        // Std (haxe./hl./sys.) namespace, not in scope above: either a compiler-magic name
        // (Array, String, Map, Iterator, ...) genuinely shared across every module, or a
        // native-ABI shell StdWrapperClassifier decided needs no wrapper - both safe to
        // reference directly, unlike a real recompiled class/enum.
        if (Naming.IsStdNamespace(name))
            return new MappedType(name, null);

        if (KnownGenericArity.TryGetValue(name, out var arity))
        {
            var args = string.Join(", ", Enumerable.Repeat("Dynamic", arity));
            return new MappedType($"{name}<{args}>", null);
        }

        return new MappedType("Dynamic", $"'{name}' not generated (excluded, invalid identifier, or filtered out)");
    }

    // Appends Dynamic-erased type args when the ORIGINAL bytecode name (never the
    // resolved path - KnownGenericArity is keyed by the bytecode-visible bare name, see
    // e.g. its own "haxe.ds.TreeNode" row) is a known generic arity - a bare reference to
    // a real generic class fails to compile ("Not enough type parameters") otherwise.
    private static string ApplyKnownGenericArity(string bytecodeName, string resolvedPath) =>
        KnownGenericArity.TryGetValue(bytecodeName, out var arity)
            ? $"{resolvedPath}<{string.Join(", ", Enumerable.Repeat("Dynamic", arity))}>"
            : resolvedPath;

    private MappedType MapEnumType(EnumType e)
    {
        var name = e.Name;
        if (knownModuleQualifiedPaths.TryGetValue(name, out var qualifiedPath))
            return new MappedType(ApplyKnownGenericArity(name, qualifiedPath), null);
        if (Naming.HasUnreferenceableSegment(name))
            return new MappedType("Dynamic", $"'{name}' is a companion/private/nested enum type");

        // Same std-safety concern as MapObjectType - a std enum is still an ordinary
        // recompiled-per-module type, not a compiler-magic one. includedEnumNames is the
        // union of EnumCollector.CandidateNames and .StdWrapperCandidateNames (see Program.cs).
        if (includedEnumNames.Contains(name) && Naming.LooksLikeValidHaxeTypePath(name))
        {
            var wrapperPath = Naming.IsStdNamespace(name) ? Naming.StdWrapperPackagePrefix + name : name;
            return new MappedType(wrapperPath, null);
        }

        // Std namespace, not in scope above: no root-magic enum names exist in practice,
        // but this is the same safe-direct-reference fallback as MapObjectType's.
        if (Naming.IsStdNamespace(name))
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

    private MappedType MapFunctionType(FunctionType f, HashSet<int> visiting, int depth)
    {
        var args = new List<string>();
        string? reason = null;
        foreach (var argIdx in f.ArgTypes)
        {
            var m = MapIndexed(argIdx, visiting, depth);
            args.Add(m.HaxeType);
            reason ??= m.FallbackReason;
        }
        var ret = MapIndexed(f.ReturnType, visiting, depth);
        reason ??= ret.FallbackReason;

        // Haxe's `->` is right-associative: a function-typed return value needs an
        // extra pair of parens or the parser flattens it into the outer arg list.
        var retTypeAtIndex = (uint)f.ReturnType < (uint)module.Types.Length ? module.Types[f.ReturnType] : null;
        var retStr = retTypeAtIndex is FunctionType ? $"({ret.HaxeType})" : ret.HaxeType;

        return new MappedType($"({string.Join(", ", args)}) -> {retStr}", reason);
    }

    private MappedType MapReferenceType(ReferenceType r, HashSet<int> visiting, int depth)
    {
        var inner = MapIndexed(r.InnerTypeIndex, visiting, depth);
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
