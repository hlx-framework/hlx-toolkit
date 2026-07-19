namespace HLX.Analysis;

public sealed class AnalysisResult
{
    public TypeNameResolver TypeNames { get; }
    public ReferenceIndex References { get; }
    public CallGraph CallGraph { get; }

    private AnalysisResult(TypeNameResolver typeNames, ReferenceIndex references, CallGraph callGraph)
    {
        TypeNames = typeNames;
        References = references;
        CallGraph = callGraph;
    }

    public static AnalysisResult Build(HlModule module) =>
        new(new TypeNameResolver(module), new ReferenceIndex(module), new CallGraph(module));
}
