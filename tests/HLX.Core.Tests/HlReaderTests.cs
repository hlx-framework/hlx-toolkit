using System.IO;
using HLX.Core;
using HLX.Core.IO;

namespace HLX.Core.Tests;

public class HlReaderTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hlboot.dat");

    private static HlModule LoadFixture()
    {
        using var fs = File.OpenRead(FixturePath);
        return HlReader.Read(fs);
    }

    [Fact]
    public void Header_VersionIs4()
    {
        var m = LoadFixture();
        Assert.Equal(4, m.Header.Version);
    }

    [Fact]
    public void Header_HasDebugInfo()
    {
        var m = LoadFixture();
        Assert.True(m.Header.Flags.HasFlag(HlFeatureFlags.HasDebugInfo));
    }

    [Fact]
    public void Ints_CountMatchesHeader()
    {
        var m = LoadFixture();
        Assert.Equal(47, m.Ints.Length);
    }

    [Fact]
    public void Floats_CountMatchesHeader()
    {
        var m = LoadFixture();
        Assert.Single(m.Floats);
    }

    [Fact]
    public void Strings_CountMatchesHeader()
    {
        var m = LoadFixture();
        Assert.Equal(382, m.Strings.Length);
    }

    [Fact]
    public void Types_CountMatchesHeader()
    {
        var m = LoadFixture();
        Assert.Equal(421, m.Types.Length);
    }

    [Fact]
    public void Globals_CountMatchesHeader()
    {
        var m = LoadFixture();
        Assert.Equal(95, m.Globals.Length);
    }

    [Fact]
    public void Natives_CountMatchesHeader()
    {
        var m = LoadFixture();
        Assert.Equal(52, m.Natives.Length);
    }

    [Fact]
    public void Functions_CountMatchesHeader()
    {
        var m = LoadFixture();
        Assert.Equal(336, m.Functions.Length);
    }

    [Fact]
    public void StringPool_ContainsKnownStrings()
    {
        var m = LoadFixture();
        // Fixture is compiled from Main.hx, which calls trace("hlx fixture")
        Assert.Contains("String", m.Strings);
        Assert.Contains("length", m.Strings);
        Assert.Contains("hlx fixture", m.Strings);
    }

    [Fact]
    public void DebugFiles_NotEmpty()
    {
        var m = LoadFixture();
        Assert.NotEmpty(m.DebugFiles);
    }

    [Fact]
    public void Types_FirstTypeIsPrimitive()
    {
        var m = LoadFixture();
        Assert.IsAssignableFrom<PrimitiveType>(m.Types[0]);
    }

    [Fact]
    public void AllTypes_NonNull()
    {
        var m = LoadFixture();
        foreach (var t in m.Types)
            Assert.NotNull(t);
    }

    [Fact]
    public void Functions_HaveInstructions()
    {
        var m = LoadFixture();
        Assert.True(m.Functions.All(f => f.Instructions.Length > 0));
    }

    [Fact]
    public void Functions_WithDebugInfo_HaveDebugPerInstruction()
    {
        var m = LoadFixture();
        foreach (var f in m.Functions)
            Assert.Equal(f.Instructions.Length, f.DebugInfo.Length);
    }

    [Fact]
    public void Natives_HaveNonEmptyNames()
    {
        var m = LoadFixture();
        foreach (var n in m.Natives)
        {
            Assert.NotEmpty(n.Lib);
            Assert.NotEmpty(n.Name);
        }
    }

    [Fact]
    public void Functions_InstructionOffsets_AreSequential()
    {
        var m = LoadFixture();
        foreach (var f in m.Functions)
            for (int i = 0; i < f.Instructions.Length; i++)
                Assert.Equal(i, f.Instructions[i].Offset);
    }
}
