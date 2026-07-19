namespace HLX.Analysis;

public enum EdgeKind
{
    Fallthrough,
    Jump,           // includes the taken-branch target of a conditional jump
    SwitchCase,     // CaseValue holds the case index
    SwitchDefault,
    Exception,      // Trap's handler-entry edge
}

public readonly record struct CfgEdge(int FromBlock, int ToBlock, EdgeKind Kind, int? CaseValue = null);
