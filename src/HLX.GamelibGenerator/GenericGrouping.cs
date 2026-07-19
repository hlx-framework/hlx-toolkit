namespace HLX.GamelibGenerator;

internal sealed class GenericGroupingResult
{
    public List<GenericGroup> Groups { get; } = [];
    public List<GameClass> Singles { get; } = [];
    // concrete instantiation full name -> (group it collapsed into, its own T argument)
    public Dictionary<string, (GenericGroup Group, string TypeArg)> Aliases { get; } = [];
}

/// <summary>
/// Detects Haxe's <c>@:generic</c> monomorphization pattern (distinct concrete
/// <c>ObjectType</c>s per instantiation, compiler-mangled names) and collapses
/// matching groups back into one generic Haxe wrapper. A naming + structural-shape
/// heuristic, not real generic metadata (bytecode has none) - single type parameter
/// only; under-collapses safely, and could in principle coincidentally merge
/// unrelated same-shaped classes (harmless, just not "really" generic).
/// </summary>
internal static class GenericGrouping
{
    public static GenericGroupingResult Run(List<GameClass> classes)
    {
        var result = new GenericGroupingResult();
        var allNames = classes.Select(c => c.FullName).ToHashSet(StringComparer.Ordinal);
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        var byKey = new Dictionary<(string Package, string Prefix), List<GameClass>>();
        foreach (var c in classes)
        {
            var shortName = c.ShortName;
            var lastUnderscore = shortName.LastIndexOf('_');
            if (lastUnderscore <= 0 || lastUnderscore == shortName.Length - 1) continue;
            var prefix = shortName[..lastUnderscore];
            var key = (c.Package, Prefix: prefix);
            if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = [];
            list.Add(c);
        }

        foreach (var ((package, prefix), members) in byKey)
        {
            if (members.Count < 2) continue;

            var fullName = string.IsNullOrEmpty(package) ? prefix : $"{package}.{prefix}";
            // Don't shadow a real class already named exactly `fullName`.
            if (allNames.Contains(fullName) && members.All(m => m.FullName != fullName)) continue;

            if (TryCollapse(fullName, members, out var group, out var aliases))
            {
                result.Groups.Add(group);
                foreach (var (concreteName, typeArg) in aliases)
                {
                    result.Aliases[concreteName] = (group, typeArg);
                    consumed.Add(concreteName);
                }
            }
        }

        foreach (var c in classes)
            if (!consumed.Contains(c.FullName))
                result.Singles.Add(c);

        return result;
    }

    private static bool TryCollapse(
        string fullName,
        List<GameClass> members,
        out GenericGroup group,
        out List<(string ConcreteName, string TypeArg)> aliases)
    {
        group = null!;
        aliases = [];

        var first = members[0];
        int fieldCount = first.Fields.Count;
        int methodCount = first.Methods.Count;

        // Methods can't collapse: a method call needs resolveMember against a known
        // concrete type NAME, but instantiations are separate HL classes with separate
        // names, and there's no way to know which one applies to `this` at the call
        // site. Field access has no such problem (resolveField is fully dynamic), so
        // only pure data-only groups are safe to collapse.
        if (methodCount != 0) return false;

        foreach (var m in members)
        {
            if (m.Fields.Count != fieldCount || m.Methods.Count != methodCount) return false;
            for (int i = 0; i < fieldCount; i++)
                if (!string.Equals(m.Fields[i].Name, first.Fields[i].Name, StringComparison.Ordinal)) return false;
            for (int i = 0; i < methodCount; i++)
            {
                if (!string.Equals(m.Methods[i].Name, first.Methods[i].Name, StringComparison.Ordinal)) return false;
                if (m.Methods[i].IsStatic != first.Methods[i].IsStatic) return false;
                if (m.Methods[i].Params.Count != first.Methods[i].Params.Count) return false;
            }
        }

        // Single type parameter means each member must resolve to one consistent "T"
        // across all its varying positions; otherwise bail and emit members independently.
        var perMemberTValue = new Dictionary<string, string>(StringComparer.Ordinal);
        bool anyVarying = false;

        bool FoldPosition(Func<GameClass, string> selector)
        {
            var values = members.Select(selector).ToList();
            if (values.Distinct(StringComparer.Ordinal).Count() <= 1) return true; // concrete, shared - not varying
            anyVarying = true;
            for (int i = 0; i < members.Count; i++)
            {
                var name = members[i].FullName;
                var v = values[i];
                if (perMemberTValue.TryGetValue(name, out var existing))
                {
                    if (!string.Equals(existing, v, StringComparison.Ordinal)) return false;
                }
                else perMemberTValue[name] = v;
            }
            return true;
        }

        for (int i = 0; i < fieldCount; i++)
        {
            var fi = i;
            if (!FoldPosition(m => m.Fields[fi].Type.HaxeType)) return false;
        }
        for (int i = 0; i < methodCount; i++)
        {
            var mi = i;
            for (int p = 0; p < first.Methods[mi].Params.Count; p++)
            {
                var pi = p;
                if (!FoldPosition(m => m.Methods[mi].Params[pi].HaxeType)) return false;
            }
            if (!FoldPosition(m => m.Methods[mi].Return.HaxeType)) return false;
        }

        // No varying position at all: nothing to parameterize, not really a generic template.
        if (!anyVarying) return false;
        // A member that never touches a varying slot is a degenerate shape for single-T. Bail.
        if (perMemberTValue.Count != members.Count) return false;

        string Sub(MappedType concrete, IReadOnlyList<string> allValuesAtThisPosition) =>
            allValuesAtThisPosition.Distinct(StringComparer.Ordinal).Count() <= 1 ? concrete.HaxeType : "T";

        var mergedFields = new List<GameField>();
        for (int i = 0; i < fieldCount; i++)
        {
            var values = members.Select(m => m.Fields[i].Type.HaxeType).ToList();
            // Only propagate a real accessor when EVERY instantiation has one - unsafe otherwise.
            var hasRealGetter = members.All(m => m.Fields[i].HasRealGetter);
            var hasRealSetter = members.All(m => m.Fields[i].HasRealSetter);
            mergedFields.Add(new GameField
            {
                Name = first.Fields[i].Name,
                Type = new MappedType(Sub(first.Fields[i].Type, values), null),
                HasRealGetter = hasRealGetter,
                HasRealSetter = hasRealSetter,
            });
        }

        var mergedMethods = new List<GameMethod>();
        for (int i = 0; i < methodCount; i++)
        {
            var ps = new List<MappedType>();
            for (int p = 0; p < first.Methods[i].Params.Count; p++)
            {
                var values = members.Select(m => m.Methods[i].Params[p].HaxeType).ToList();
                ps.Add(new MappedType(Sub(first.Methods[i].Params[p], values), null));
            }
            var retValues = members.Select(m => m.Methods[i].Return.HaxeType).ToList();
            var ret = new MappedType(Sub(first.Methods[i].Return, retValues), null);
            mergedMethods.Add(new GameMethod { Name = first.Methods[i].Name, IsStatic = first.Methods[i].IsStatic, Params = ps, Return = ret });
        }

        group = new GenericGroup { FullName = fullName, Instantiations = members.Select(m => m.FullName).ToList() };
        group.Fields.AddRange(mergedFields);
        group.Methods.AddRange(mergedMethods);
        aliases = members.Select(m => (m.FullName, perMemberTValue[m.FullName])).ToList();
        return true;
    }
}
