using HLX.Core;

namespace HLX.GamelibGenerator;

/// <summary>
/// Walks <see cref="HlModule.Types"/> for in-scope <c>ObjectType</c> entries and
/// derives each one's instance and static (<c>$ClassName</c> companion) members.
/// </summary>
internal sealed class ClassCollector
{
    private readonly HlModule _module;
    private HaxeTypeMapper _mapper = null!;
    private readonly Dictionary<string, int> _objectTypeIndexByName = [];
    private readonly Dictionary<int, FunctionType> _functionTypeByIndex = [];
    private readonly IReadOnlyDictionary<int, int> _constructorFindexByTypeIndex;

    public List<GameClass> Classes { get; } = [];
    public HashSet<string> CandidateNames { get; } = [];

    // Std (haxe./hl./sys.) names StdWrapperClassifier decided need a generated wrapper -
    // built via the exact same BuildClass machinery as CandidateNames, just kept in a
    // separate set since their OUTPUT PATH differs (see Program.cs's std rename step,
    // which renames each one's FullName to "hlx.std.<real name>" after CollectAll).
    public HashSet<string> StdWrapperCandidateNames { get; } = [];

    public int TotalObjectTypes;
    public int CompanionTypes;
    public int ExcludedByNamespace;
    public int ExcludedUnreferenceable;
    public int ExcludedInvalidIdentifier;
    public int ExcludedTooLong;
    public int SkippedMembers;
    public int ClassesWithConstructor;

    public ClassCollector(HlModule module, IReadOnlyDictionary<int, int> constructorFindexByTypeIndex)
    {
        _module = module;
        _constructorFindexByTypeIndex = constructorFindexByTypeIndex;

        for (int i = 0; i < module.Types.Length; i++)
            if (module.Types[i] is ObjectType o)
            {
                TotalObjectTypes++;
                _objectTypeIndexByName[o.Name] = i;
            }

        // A proto/binding's FunctionIndex can land in either table.
        foreach (var n in module.Natives) _functionTypeByIndex[n.FunctionIndex] = n.Type;
        foreach (var f in module.Functions) _functionTypeByIndex[f.FunctionIndex] = f.Type;

        // A companion's `$` prefix is on its LAST segment only (e.g. "hxd.res.$DefaultFont").
        foreach (var (name, idx) in _objectTypeIndexByName)
        {
            if (Naming.ShortName(name).StartsWith('$')) { CompanionTypes++; continue; }
            ClassifyCandidate(name, idx);
        }
    }

    // Routes a std-namespace name through StdWrapperClassifier instead of dropping it
    // outright - the only difference from an ordinary game class is WHICH set it lands
    // in (and, for std, that the companion's own static members can also justify a wrap).
    private void ClassifyCandidate(string name, int idx)
    {
        if (Naming.HasUnreferenceableSegment(name)) { ExcludedUnreferenceable++; return; }
        if (!Naming.LooksLikeValidHaxeTypePath(name)) { ExcludedInvalidIdentifier++; return; }
        if (!Naming.IsReasonableLength(name)) { ExcludedTooLong++; return; }

        if (Naming.IsStdNamespace(name))
        {
            // Root-magic names (Array, String, Map, ...) are genuinely shared across
            // every module - always referenced directly, never generated. Same for a
            // name HaxeTypeMapper always resolves via an earlier special case (real
            // Array<T> backing types, module-qualified secondary-type renames) -
            // wrapping one of those would be dead output nothing ever references.
            if (Naming.IsRootStdlibMagicName(name)) { ExcludedByNamespace++; return; }
            if (HaxeTypeMapper.IsAlwaysResolvedBeforeWrapCheck(name)) { ExcludedByNamespace++; return; }

            var o = (ObjectType)_module.Types[idx];
            if (StdWrapperClassifier.NeedsWrapper(o, LookupCompanion(name)))
                StdWrapperCandidateNames.Add(name);
            else
                ExcludedByNamespace++; // native-ABI shell - safe to reference directly, same as a magic name.
            return;
        }

        CandidateNames.Add(name);
    }

    private static string CompanionNameOf(string name) =>
        Naming.PackageOf(name) is "" ? "$" + name : Naming.PackageOf(name) + ".$" + Naming.ShortName(name);

    private ObjectType? LookupCompanion(string name) =>
        _objectTypeIndexByName.TryGetValue(CompanionNameOf(name), out var idx) && _module.Types[idx] is ObjectType companion
            ? companion
            : null;

    // Requires both collectors' candidate names already collected (see Program.cs's sequencing).
    // Std wrapper candidates are built via the exact same BuildClass machinery as ordinary
    // classes - Program.cs renames their FullName/RuntimeTypeName afterward (step 2 of the
    // std-wrapper refactor), before grouping/emission.
    public void CollectAll(HaxeTypeMapper mapper)
    {
        _mapper = mapper;
        foreach (var name in CandidateNames.Concat(StdWrapperCandidateNames).OrderBy(n => n, StringComparer.Ordinal))
        {
            var idx = _objectTypeIndexByName[name];
            var o = (ObjectType)_module.Types[idx];
            Classes.Add(BuildClass(name, idx, o));
        }
    }

    private GameClass BuildClass(string name, int idx, ObjectType o)
    {
        var gc = new GameClass { FullName = name, TypeIndex = idx, ParentFullName = ResolveGeneratedParent(o) };
        // Shared between instance and static members: Haxe has one class namespace
        // for both.
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        // Real proto names, to tell a compiled get_/set_ accessor apart from a plain data field.
        var protoNames = new HashSet<string>(o.Protos.Select(p => p.Name), StringComparer.Ordinal);

        // This class's OWN Fields only - never flattens the ancestor chain.
        foreach (var f in o.Fields)
        {
            if (!TryClaimField(usedNames, f.Name, gc)) continue;
            gc.Fields.Add(new GameField
            {
                Name = f.Name,
                Type = _mapper.Map(f.TypeIndex),
                HasRealGetter = protoNames.Contains("get_" + f.Name),
                HasRealSetter = protoNames.Contains("set_" + f.Name),
            });
        }

        var instanceFieldNames = new HashSet<string>(o.Fields.Select(f => f.Name), StringComparer.Ordinal);
        foreach (var p in o.Protos)
        {
            // Routed through the field's own (get, set) property instead of a standalone method.
            if (IsRoutedAccessorName(p.Name, instanceFieldNames))
            {
                gc.Notes.Add($"instance method '{p.Name}': real compiled property accessor - routed through its field's own (get, set) wrapper property instead of a standalone method");
                continue;
            }
            if (!TryClaimMethod(usedNames, p.Name, gc)) continue;
            if (!_functionTypeByIndex.TryGetValue(p.FunctionIndex, out var ft))
            {
                gc.Notes.Add($"instance method '{p.Name}': findex {p.FunctionIndex} not found in module - skipped");
                SkippedMembers++;
                continue;
            }
            var (ps, ret) = _mapper.MapCallable(ft, dropSelf: true);
            gc.Methods.Add(new GameMethod { Name = p.Name, IsStatic = false, Params = ps, Return = ret });
        }

        // Static members live on a "$ClassName" companion type.
        if (LookupCompanion(name) is { } companion)
        {
            CollectStaticMembers(companion, gc, usedNames);
        }

        // Absent means zero/ambiguous constructor candidates, not an error.
        if (_constructorFindexByTypeIndex.TryGetValue(idx, out var ctorFindex) &&
            _functionTypeByIndex.TryGetValue(ctorFindex, out var ctorFt))
        {
            // "create" collision check: some real classes (e.g. mpman.client.MPLobby) already have one.
            if (usedNames.Add("create"))
            {
                var (ctorParams, _) = _mapper.MapCallable(ctorFt, dropSelf: true);
                gc.Constructor = new GameConstructor { Findex = ctorFindex, Params = ctorParams };
                ClassesWithConstructor++;
            }
            else
            {
                gc.Notes.Add($"real constructor (findex {ctorFindex}) recovered but this class already has its own real 'create' member - factory skipped to avoid a name collision");
                SkippedMembers++;
            }
        }

        return gc;
    }

    // Only the direct super: @:forward composes transitively through the parent's own wrapper.
    // A std wrapper candidate can chain onto another std wrapper candidate too (e.g. a std
    // subclass extending a std base class), same as ordinary game classes - but the parent's
    // OWN generated file lives under its renamed "hlx.std.<name>" path (see Program.cs's std
    // rename step, which runs after CollectAll), so ParentFullName must anticipate that rename
    // here instead of using the parent's raw bytecode name - referencing the real std class
    // directly as this abstract's underlying type would be exactly the cross-module SafeCast
    // bug the wrapper exists to dodge (and, for a real generic std class like
    // haxe.ds.BalancedTree, also just fails to compile - "Not enough type parameters").
    private string? ResolveGeneratedParent(ObjectType o)
    {
        if (o.SuperIndex is not { } superIdx) return null;
        if (_module.Types[superIdx] is not ObjectType super) return null;
        if (CandidateNames.Contains(super.Name)) return super.Name;
        if (StdWrapperCandidateNames.Contains(super.Name)) return Naming.StdWrapperPackagePrefix + super.Name;
        return null;
    }

    private void CollectStaticMembers(ObjectType companion, GameClass gc, HashSet<string> usedNames)
    {
        // Companion Bindings field indices are flattened across the super chain - offset accordingly.
        int offset = 0;
        var superIdx = companion.SuperIndex;
        while (superIdx.HasValue && _module.Types[superIdx.Value] is ObjectType super)
        {
            offset += super.Fields.Length;
            superIdx = super.SuperIndex;
        }

        var bindingByField = new Dictionary<int, int>();
        foreach (var b in companion.Bindings) bindingByField[b.FieldIndex] = b.FunctionIndex;

        // A real static property compiles to BOTH an unbound data field and a bound get_/set_ field.
        var boundFieldNames = new HashSet<string>(StringComparer.Ordinal);
        var unboundFieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < companion.Fields.Length; i++)
        {
            var fieldName = companion.Fields[i].Name;
            if (bindingByField.ContainsKey(offset + i)) boundFieldNames.Add(fieldName);
            else unboundFieldNames.Add(fieldName);
        }

        for (int i = 0; i < companion.Fields.Length; i++)
        {
            var field = companion.Fields[i];
            // "__constructor__" is not a real reflective slot (see ConstructorCollector).
            if (field.Name == "__constructor__") continue;

            int globalIndex = offset + i;
            if (!bindingByField.TryGetValue(globalIndex, out var findex))
            {
                if (!TryClaimStaticField(usedNames, field.Name, gc)) continue;
                gc.StaticFields.Add(new GameStaticField
                {
                    Name = field.Name,
                    Type = _mapper.Map(field.TypeIndex),
                    HasRealGetter = boundFieldNames.Contains("get_" + field.Name),
                    HasRealSetter = boundFieldNames.Contains("set_" + field.Name),
                });
                continue;
            }
            // Routed through the static field's own (get, set) property instead of a standalone method.
            if (IsRoutedAccessorName(field.Name, unboundFieldNames))
            {
                gc.Notes.Add($"static method '{field.Name}': real compiled property accessor - routed through its static field's own (get, set) wrapper property instead of a standalone method");
                continue;
            }
            if (!_functionTypeByIndex.TryGetValue(findex, out var ft))
            {
                gc.Notes.Add($"static member '{field.Name}': bound findex {findex} not found in module - skipped");
                SkippedMembers++;
                continue;
            }
            if (!TryClaimMethod(usedNames, field.Name, gc)) continue;
            // Static-bound functions carry no implicit receiver.
            var (ps, ret) = _mapper.MapCallable(ft, dropSelf: false);
            gc.Methods.Add(new GameMethod { Name = field.Name, IsStatic = true, Params = ps, Return = ret });
        }
    }

    // True when `name` is exactly get_<X>/set_<X> for some other field X already in fieldNames.
    private static bool IsRoutedAccessorName(string name, HashSet<string> fieldNames)
    {
        if (name.Length > 4 && name.StartsWith("get_", StringComparison.Ordinal) && fieldNames.Contains(name[4..]))
            return true;
        if (name.Length > 4 && name.StartsWith("set_", StringComparison.Ordinal) && fieldNames.Contains(name[4..]))
            return true;
        return false;
    }

    private bool TryClaimField(HashSet<string> used, string name, GameClass gc)
    {
        if (!Naming.IsValidPlainIdentifier(name))
        {
            gc.Notes.Add($"field '{name}': not a usable plain Haxe identifier (reserved word or invalid characters) - skipped");
            SkippedMembers++;
            return false;
        }
        // Also reserve get_<name>/set_<name>, since this field emits a real (get, set).
        var getter = "get_" + name;
        var setter = "set_" + name;
        if (used.Contains(name) || used.Contains(getter) || used.Contains(setter))
        {
            gc.Notes.Add($"field '{name}': name (or its generated accessor '{getter}'/'{setter}') collides with an already-declared member - skipped");
            SkippedMembers++;
            return false;
        }
        used.Add(name);
        used.Add(getter);
        used.Add(setter);
        return true;
    }

    private bool TryClaimStaticField(HashSet<string> used, string name, GameClass gc)
    {
        if (!Naming.IsValidPlainIdentifier(name))
        {
            gc.Notes.Add($"static field '{name}': not a usable plain Haxe identifier (reserved word or invalid characters) - skipped");
            SkippedMembers++;
            return false;
        }
        var getter = "get_" + name;
        var setter = "set_" + name;
        if (used.Contains(name) || used.Contains(getter) || used.Contains(setter))
        {
            gc.Notes.Add($"static field '{name}': name (or its generated accessor '{getter}'/'{setter}') collides with an already-declared member - skipped");
            SkippedMembers++;
            return false;
        }
        used.Add(name);
        used.Add(getter);
        used.Add(setter);
        return true;
    }

    private bool TryClaimMethod(HashSet<string> used, string name, GameClass gc)
    {
        if (!Naming.IsValidPlainIdentifier(name))
        {
            gc.Notes.Add($"method '{name}': not a usable plain Haxe identifier (reserved word or invalid characters) - skipped");
            SkippedMembers++;
            return false;
        }
        if (!used.Add(name))
        {
            gc.Notes.Add($"method '{name}': name collides with an already-declared member - skipped");
            SkippedMembers++;
            return false;
        }
        return true;
    }
}
