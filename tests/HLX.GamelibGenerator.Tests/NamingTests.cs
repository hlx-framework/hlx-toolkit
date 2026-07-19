using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

// Pure string -> bool functions - no bytecode/fixture needed. Exhaustive
// edge-case coverage that's awkward or impossible to force reliably through
// a real compiled Haxe program (reserved words, malformed identifiers,
// pathological lengths, stdlib-magic-name collisions).
public class NamingTests
{
    [Theory]
    [InlineData("Void")]
    [InlineData("Int")]
    [InlineData("String")]
    [InlineData("Map")]
    [InlineData("ArrayAccess")]
    [InlineData("Class")] // distinct from packaged secondary "hl.Class"
    public void IsRootStdlibMagicName_MatchesFixedRootNames(string name) =>
        Assert.True(Naming.IsRootStdlibMagicName(name));

    [Theory]
    [InlineData("Widget")]
    [InlineData("game.Player")]
    [InlineData("Sub")]
    public void IsRootStdlibMagicName_RejectsOrdinaryNames(string name) =>
        Assert.False(Naming.IsRootStdlibMagicName(name));

    [Theory]
    [InlineData("haxe.ds.StringMap")]
    [InlineData("hl.Bytes")]
    [InlineData("sys.io.File")]
    [InlineData("Void")]
    [InlineData("Map")]
    public void IsExcludedNamespace_MatchesStdlibPrefixesAndMagicNames(string name) =>
        Assert.True(Naming.IsExcludedNamespace(name));

    [Theory]
    [InlineData("game.Player")]
    [InlineData("hxd.res.DefaultFont")]
    [InlineData("h2d.Object")]
    [InlineData("Widget")]
    public void IsExcludedNamespace_DoesNotMatchGameOrEngineNamespaces(string name) =>
        Assert.False(Naming.IsExcludedNamespace(name));

    [Theory]
    [InlineData("_Main.Local", true)]      // leading underscore segment
    [InlineData("pkg.$Sub", true)]         // companion "$" segment
    [InlineData("$Widget", true)]
    [InlineData("pkg..Sub", true)]         // empty segment
    [InlineData("game.Player", false)]
    [InlineData("Widget", false)]
    public void HasUnreferenceableSegment(string name, bool expected) =>
        Assert.Equal(expected, Naming.HasUnreferenceableSegment(name));

    [Theory]
    [InlineData("game.Player", true)]
    [InlineData("Widget", true)]
    [InlineData("fixture.pkg.Sub", true)]
    [InlineData("hl_random", false)]          // lowercase final segment - not a real type path
    [InlineData("game.player", false)]        // lowercase final segment
    [InlineData("", false)]
    [InlineData("game.Pl@yer", false)]        // invalid identifier character
    public void LooksLikeValidHaxeTypePath(string name, bool expected) =>
        Assert.Equal(expected, Naming.LooksLikeValidHaxeTypePath(name));

    [Fact]
    public void IsReasonableLength_RejectsPathologicallyLongNames()
    {
        var reasonable = new string('A', 180);
        var tooLong = new string('A', 181);
        Assert.True(Naming.IsReasonableLength(reasonable));
        Assert.False(Naming.IsReasonableLength(tooLong));
    }

    [Theory]
    [InlineData("class", true)]
    [InlineData("new", true)]
    [InlineData("static", true)]
    [InlineData("value", false)]
    [InlineData("Widget", false)]
    public void IsReservedWord(string name, bool expected) =>
        Assert.Equal(expected, Naming.IsReservedWord(name));

    [Theory]
    [InlineData("value", true)]
    [InlineData("class", false)]         // reserved word
    [InlineData("2bad", false)]          // can't start with a digit
    [InlineData("has space", false)]
    [InlineData("", false)]
    public void IsValidPlainIdentifier(string name, bool expected) =>
        Assert.Equal(expected, Naming.IsValidPlainIdentifier(name));

    [Theory]
    [InlineData("game.ui.Widget", "Widget")]
    [InlineData("Widget", "Widget")]
    [InlineData("fixture.pkg.Sub", "Sub")]
    public void ShortName(string full, string expected) =>
        Assert.Equal(expected, Naming.ShortName(full));

    [Theory]
    [InlineData("game.ui.Widget", "game.ui")]
    [InlineData("Widget", "")]
    [InlineData("fixture.pkg.Sub", "fixture.pkg")]
    public void PackageOf(string full, string expected) =>
        Assert.Equal(expected, Naming.PackageOf(full));
}
