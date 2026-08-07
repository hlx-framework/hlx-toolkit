using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

public class EnumCollectorTests
{
    [Fact]
    public void Direction_HasFourZeroArgConstructorsInDeclaredOrder()
    {
        var e = Fixture.FindEnum("Direction");
        Assert.Equal(["North", "South", "East", "West"], e.Constructors.Select(c => c.Name));
        Assert.All(e.Constructors, c => Assert.Empty(c.ParamTypes));
        // Index matches real EnumType.Constructs position (what Type.enumIndex() reads), never a name-based lookup.
        Assert.Equal([0, 1, 2, 3], e.Constructors.Select(c => c.Index));
    }

    [Fact]
    public void Direction_UnusedConstructors_SurviveOnlyBecauseOfDceNo()
    {
        // East/West are never constructed in the fixture; confirms -dce no is doing real work.
        var e = Fixture.FindEnum("Direction");
        Assert.Contains(e.Constructors, c => c.Name == "East");
        Assert.Contains(e.Constructors, c => c.Name == "West");
    }

    [Fact]
    public void GameEvent_ZeroArgConstructor_HasNoParams()
    {
        var e = Fixture.FindEnum("GameEvent");
        var started = e.Constructors.Single(c => c.Name == "Started");
        Assert.Empty(started.ParamTypes);
    }

    [Fact]
    public void GameEvent_IntAndStringParams()
    {
        var e = Fixture.FindEnum("GameEvent");
        var c = e.Constructors.Single(x => x.Name == "ScoreChanged");
        Assert.Equal(["Int", "String"], c.ParamTypes.Select(p => p.HaxeType));
    }

    [Fact]
    public void GameEvent_FloatParams()
    {
        var e = Fixture.FindEnum("GameEvent");
        var c = e.Constructors.Single(x => x.Name == "PositionUpdated");
        Assert.Equal(["Float", "Float"], c.ParamTypes.Select(p => p.HaxeType));
    }

    [Fact]
    public void GameEvent_NestedEnumParam_ResolvesToGeneratedWrapper_NotDynamic()
    {
        var e = Fixture.FindEnum("GameEvent");
        var c = e.Constructors.Single(x => x.Name == "DirectionChosen");
        var param = Assert.Single(c.ParamTypes);
        Assert.Equal("Direction", param.HaxeType);
        Assert.Null(param.FallbackReason);
    }

    [Fact]
    public void GameEvent_ArrayParam_ArrayOfStringIsBackedByArrayObj_ErasesToArrayBase()
    {
        // Array<String> compiles to hl.types.ArrayObj (concrete, non-Dynamic elements), not
        // hl.types.ArrayDyn - the real element type is erased at this level (HL shares one
        // native array class per storage kind, not per element type), so this is honestly
        // hl.types.ArrayBase (cross-module-safe, gives .length/.getDyn(i)) with a fallback
        // reason rather than a false-precision Array<Dynamic>.
        var e = Fixture.FindEnum("GameEvent");
        var c = e.Constructors.Single(x => x.Name == "ItemsCollected");
        var param = Assert.Single(c.ParamTypes);
        Assert.Equal("hl.types.ArrayBase", param.HaxeType);
        Assert.NotNull(param.FallbackReason);
    }

    [Fact]
    public void GameEvent_UnusedConstructor_SurvivesOnlyBecauseOfDceNo()
    {
        var e = Fixture.FindEnum("GameEvent");
        Assert.Contains(e.Constructors, c => c.Name == "ItemsCollected");
    }

    [Fact]
    public void CandidateNames_IncludeBothFixtureEnums_ExcludeStdlibEnums()
    {
        var names = Fixture.Get().Enums.CandidateNames;
        Assert.Contains("Direction", names);
        Assert.Contains("GameEvent", names);
        Assert.DoesNotContain(names, n => n.StartsWith("haxe.", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.StartsWith("hl.", StringComparison.Ordinal));
    }
}
