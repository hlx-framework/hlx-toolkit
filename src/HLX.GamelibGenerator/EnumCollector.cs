using HLX.Core;

namespace HLX.GamelibGenerator;

/// <summary>
/// Walks <see cref="HlModule.Types"/> for in-scope <c>EnumType</c> entries and builds
/// each one's constructor list from <c>EnumType.Constructs</c>.
/// </summary>
internal sealed class EnumCollector
{
    private readonly HlModule _module;
    private readonly Dictionary<string, int> _enumTypeIndexByName = [];

    public List<GameEnum> Enums { get; } = [];
    public HashSet<string> CandidateNames { get; } = [];

    // Std (haxe./hl./sys.) enum names - a compiled EnumType always needs a wrapper (see
    // StdWrapperClassifier.NeedsWrapper(EnumType)), so this always ends up holding every
    // std-namespace name found here. Kept separate from CandidateNames for the same
    // output-path reason as ClassCollector.StdWrapperCandidateNames.
    public HashSet<string> StdWrapperCandidateNames { get; } = [];

    public int TotalEnumTypes;
    public int ExcludedByNamespace;
    public int ExcludedUnreferenceable;
    public int ExcludedInvalidIdentifier;
    public int ExcludedTooLong;

    public EnumCollector(HlModule module)
    {
        _module = module;

        for (int i = 0; i < module.Types.Length; i++)
            if (module.Types[i] is EnumType e)
            {
                TotalEnumTypes++;
                _enumTypeIndexByName[e.Name] = i;
            }

        foreach (var (name, idx) in _enumTypeIndexByName)
            ClassifyCandidate(name, (EnumType)module.Types[idx]);
    }

    // Mirrors ClassCollector.ClassifyCandidate's checks.
    private void ClassifyCandidate(string name, EnumType e)
    {
        if (Naming.HasUnreferenceableSegment(name)) { ExcludedUnreferenceable++; return; }
        if (!Naming.LooksLikeValidHaxeTypePath(name)) { ExcludedInvalidIdentifier++; return; }
        if (!Naming.IsReasonableLength(name)) { ExcludedTooLong++; return; }

        if (Naming.IsStdNamespace(name))
        {
            // No root-magic enum names exist in practice, but stay consistent with
            // ClassCollector's own check rather than assuming that.
            if (Naming.IsRootStdlibMagicName(name)) { ExcludedByNamespace++; return; }
            // Same dead-output concern as ClassCollector's own check - a name
            // HaxeTypeMapper always resolves via KnownModuleQualifiedPaths before ever
            // reaching the wrap check would never actually get referenced as a wrapper.
            if (HaxeTypeMapper.IsAlwaysResolvedBeforeWrapCheck(name)) { ExcludedByNamespace++; return; }
            if (StdWrapperClassifier.NeedsWrapper(e))
                StdWrapperCandidateNames.Add(name);
            else
                ExcludedByNamespace++;
            return;
        }

        CandidateNames.Add(name);
    }

    public void CollectAll(HaxeTypeMapper mapper)
    {
        foreach (var name in CandidateNames.Concat(StdWrapperCandidateNames).OrderBy(n => n, StringComparer.Ordinal))
        {
            var idx = _enumTypeIndexByName[name];
            var e = (EnumType)_module.Types[idx];
            Enums.Add(BuildEnum(mapper, name, idx, e));
        }
    }

    private static GameEnum BuildEnum(HaxeTypeMapper mapper, string name, int idx, EnumType e)
    {
        var ge = new GameEnum { FullName = name, TypeIndex = idx };
        for (int i = 0; i < e.Constructs.Length; i++)
        {
            var c = e.Constructs[i];
            var paramTypes = c.ParamTypes.Select(mapper.Map).ToList();
            ge.Constructors.Add(new GameEnumConstructor { Name = c.Name, Index = i, ParamTypes = paramTypes });
        }
        return ge;
    }
}
