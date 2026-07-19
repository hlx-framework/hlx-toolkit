using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

// Loads gamelib-fixture.hl once and drives the same pipeline Program.cs runs, so tests assert against real output.
internal static class Fixture
{
    public sealed record Loaded(
        HlModule Module,
        ConstructorCollector Ctors,
        ClassCollector Classes,
        EnumCollector Enums,
        HaxeTypeMapper Mapper,
        GenericGroupingResult Grouping);

    private static readonly Lazy<Loaded> _loaded = new(Load);

    public static Loaded Get() => _loaded.Value;

    private static Loaded Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "gamelib-fixture.hl");
        HlModule module;
        using (var fs = File.OpenRead(path))
            module = HlReader.Read(fs);

        var ctors = new ConstructorCollector(module);
        var classes = new ClassCollector(module, ctors.ConstructorFindexByTypeIndex);
        var enums = new EnumCollector(module);
        var mapper = new HaxeTypeMapper(module, classes.CandidateNames, enums.CandidateNames);
        classes.CollectAll(mapper);
        enums.CollectAll(mapper);
        var grouping = GenericGrouping.Run(classes.Classes);

        return new Loaded(module, ctors, classes, enums, mapper, grouping);
    }

    public static GameClass FindClass(string fullName) =>
        Get().Classes.Classes.Single(c => c.FullName == fullName);

    public static GameEnum FindEnum(string fullName) =>
        Get().Enums.Enums.Single(e => e.FullName == fullName);

    public static GameField Field(this GameClass c, string name) =>
        c.Fields.Single(f => f.Name == name);

    public static GameStaticField StaticField(this GameClass c, string name) =>
        c.StaticFields.Single(f => f.Name == name);

    public static GameMethod Method(this GameClass c, string name) =>
        c.Methods.Single(m => m.Name == name);
}
