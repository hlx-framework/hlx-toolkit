using System.IO;
using HLX.Analysis;
using HLX.Core;
using HLX.Core.IO;

namespace HLX.Analysis.Tests;

public class AnalysisTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hlboot.dat");

    private static HlModule LoadFixture()
    {
        using var fs = File.OpenRead(FixturePath);
        return HlReader.Read(fs);
    }

    [Fact]
    public void Build_DoesNotThrow()
    {
        var m = LoadFixture();
        var result = AnalysisResult.Build(m);
        Assert.NotNull(result.TypeNames);
        Assert.NotNull(result.References);
        Assert.NotNull(result.CallGraph);
    }

    [Fact]
    public void TypeNameResolver_AllTypesHaveNonEmptyNames()
    {
        var m = LoadFixture();
        var resolver = new TypeNameResolver(m);
        for (int i = 0; i < m.Types.Length; i++)
            Assert.False(string.IsNullOrEmpty(resolver.Resolve(i)),
                $"Type #{i} ({m.Types[i].GetType().Name}) resolved to empty string");
    }

    [Fact]
    public void TypeNameResolver_PrimitiveVoidIsVoid()
    {
        var m = LoadFixture();
        var resolver = new TypeNameResolver(m);
        // First type in HL is always void
        Assert.Equal("void", resolver.Resolve(0));
    }

    [Fact]
    public void TypeNameResolver_ObjectTypeUsesName()
    {
        var m = LoadFixture();
        var resolver = new TypeNameResolver(m);
        int stringIdx = m.Types
            .Select((t, i) => (t, i))
            .First(x => x.t is ObjectType o && o.Name == "String")
            .i;
        Assert.Equal("String", resolver.Resolve(stringIdx));
    }

    [Fact]
    public void TypeNameResolver_FunctionTypeHasFunPrefix()
    {
        var m = LoadFixture();
        var resolver = new TypeNameResolver(m);
        int funIdx = m.Types
            .Select((t, i) => (t, i))
            .First(x => x.t is FunctionType f && !f.IsMethod)
            .i;
        Assert.StartsWith("fun(", resolver.Resolve(funIdx));
    }

    [Fact]
    public void TypeNameResolver_OutOfRangeIndexReturnsQuestionMark()
    {
        var m = LoadFixture();
        var resolver = new TypeNameResolver(m);
        Assert.Equal("?9999", resolver.Resolve(9999));
    }

    [Fact]
    public void CallGraph_IsNonEmpty()
    {
        var m = LoadFixture();
        var graph = new CallGraph(m);
        Assert.True(graph.IsNonEmpty, "Expected at least one function to call another");
    }

    [Fact]
    public void CallGraph_SomeFunctionHasCallers()
    {
        var m = LoadFixture();
        var graph = new CallGraph(m);
        bool anyCallers = m.Functions
            .Any(fn => !graph.Callers(fn.FunctionIndex).IsEmpty);
        Assert.True(anyCallers, "Expected at least one function to have callers");
    }

    [Fact]
    public void CallGraph_CallerCalleeLinkIsConsistent()
    {
        var m = LoadFixture();
        var graph = new CallGraph(m);
        foreach (var fn in m.Functions)
        {
            foreach (int callee in graph.Callees(fn.FunctionIndex))
            {
                Assert.Contains(fn.FunctionIndex, graph.Callers(callee));
            }
        }
    }

    [Fact]
    public void CallGraph_EmptyForUnreferencedFindex()
    {
        var m = LoadFixture();
        var graph = new CallGraph(m);
        Assert.True(graph.Callees(-1).IsEmpty);
        Assert.True(graph.Callers(-1).IsEmpty);
    }

    [Fact]
    public void ReferenceIndex_HasEntries()
    {
        var m = LoadFixture();
        var index = new ReferenceIndex(m);
        Assert.True(index.IndexedTypeCount > 0,
            "Expected at least one type to appear in TypeIndex instruction operands");
    }

    [Fact]
    public void ReferenceIndex_ReferencedTypeHasNonEmptyFunctionList()
    {
        var m = LoadFixture();
        var index = new ReferenceIndex(m);
        int referencedType = Enumerable.Range(0, m.Types.Length)
            .First(i => !index.FunctionsReferencingType(i).IsEmpty);
        var fns = index.FunctionsReferencingType(referencedType);
        Assert.NotEmpty(fns);
        int nativeCount = m.Natives.Length;
        foreach (int findex in fns)
            Assert.True(findex >= nativeCount && findex < nativeCount + m.Functions.Length,
                $"findex {findex} is not a valid function index");
    }

    [Fact]
    public void ReferenceIndex_UnreferencedTypeReturnsEmpty()
    {
        var m = LoadFixture();
        var index = new ReferenceIndex(m);
        Assert.True(index.FunctionsReferencingType(99999).IsEmpty);
    }

    [Fact]
    public void ReferenceIndex_ResultsAreSorted()
    {
        var m = LoadFixture();
        var index = new ReferenceIndex(m);
        int referencedType = Enumerable.Range(0, m.Types.Length)
            .First(i => !index.FunctionsReferencingType(i).IsEmpty);
        var fns = index.FunctionsReferencingType(referencedType);
        for (int i = 1; i < fns.Length; i++)
            Assert.True(fns[i - 1] < fns[i], "FunctionIndex list should be sorted ascending");
    }
}
