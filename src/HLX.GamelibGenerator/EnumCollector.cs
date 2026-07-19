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

        foreach (var (name, _) in _enumTypeIndexByName)
            if (IsCandidateEnum(name)) CandidateNames.Add(name);
    }

    // Mirrors ClassCollector.IsCandidateClass's checks.
    private bool IsCandidateEnum(string name)
    {
        if (Naming.HasUnreferenceableSegment(name)) { ExcludedUnreferenceable++; return false; }
        if (Naming.IsExcludedNamespace(name)) { ExcludedByNamespace++; return false; }
        if (!Naming.LooksLikeValidHaxeTypePath(name)) { ExcludedInvalidIdentifier++; return false; }
        if (!Naming.IsReasonableLength(name)) { ExcludedTooLong++; return false; }
        return true;
    }

    public void CollectAll(HaxeTypeMapper mapper)
    {
        foreach (var name in CandidateNames.OrderBy(n => n, StringComparer.Ordinal))
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
