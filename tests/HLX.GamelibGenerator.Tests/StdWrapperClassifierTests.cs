using HLX.GamelibGenerator;

namespace HLX.GamelibGenerator.Tests;

// Pure bytecode-shape checks - no compiler invocation or JSON needed, unlike the old
// StdClassClassifier this replaces (see JsonStdApiExtractorTests/JsonTypeMapperTests'
// own now-deleted coverage of that pipeline).
public class StdWrapperClassifierTests
{
    private static ObjectType Obj(string name, ImmutableArray<HlField> fields = default, ImmutableArray<HlProto> protos = default) =>
        new(name, null, 0, fields.IsDefault ? [] : fields, protos.IsDefault ? [] : protos, []);

    [Fact]
    public void EnumType_AlwaysNeedsWrapper()
    {
        // A compiled EnumType only exists if it has real constructors - no "shell" case.
        Assert.True(StdWrapperClassifier.NeedsWrapper(new EnumType("haxe.io.Encoding", 0, [])));
    }

    [Fact]
    public void ObjectType_WithOwnFields_NeedsWrapper()
    {
        var o = Obj("haxe.ds.StringMap", fields: [new HlField("h", 0)]);
        Assert.True(StdWrapperClassifier.NeedsWrapper(o, companion: null));
    }

    [Fact]
    public void ObjectType_WithOwnProtos_NeedsWrapper()
    {
        var o = Obj("haxe.io.Bytes", protos: [new HlProto("get", 0, -1)]);
        Assert.True(StdWrapperClassifier.NeedsWrapper(o, companion: null));
    }

    [Fact]
    public void ObjectType_NoOwnMembersAndNoCompanion_IsNativeAbiShell_DoesNotNeedWrapper()
    {
        // e.g. sys.thread.Thread: an externally-backed handle with nothing of its own
        // compiled into this module - safe to reference directly.
        var o = Obj("sys.thread.Thread");
        Assert.False(StdWrapperClassifier.NeedsWrapper(o, companion: null));
    }

    [Fact]
    public void ObjectType_NoOwnMembers_ButCompanionHasStaticFields_NeedsWrapper()
    {
        var o = Obj("haxe.Timer");
        var companion = Obj("$haxe.Timer", fields: [new HlField("stamp", 0)]);
        Assert.True(StdWrapperClassifier.NeedsWrapper(o, companion));
    }

    [Fact]
    public void ObjectType_NoOwnMembers_ButCompanionHasBindings_NeedsWrapper()
    {
        var o = Obj("haxe.Timer");
        var companion = new ObjectType("$haxe.Timer", null, 0, [new HlField("delay", 0)], [], [new HlBinding(0, 5)]);
        Assert.True(StdWrapperClassifier.NeedsWrapper(o, companion));
    }

    [Fact]
    public void ObjectType_NoOwnMembers_CompanionAlsoEmpty_DoesNotNeedWrapper()
    {
        var o = Obj("sys.thread.Thread");
        var companion = Obj("$sys.thread.Thread");
        Assert.False(StdWrapperClassifier.NeedsWrapper(o, companion));
    }
}
