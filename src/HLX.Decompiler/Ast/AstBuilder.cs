namespace HLX.Decompiler;

// Structures the CFG into if/else/while/switch; anything not recognized falls
// back to a flat label/goto dump (FlattenBlocks) so no instruction is ever
// silently dropped.
public sealed class AstBuilder
{
    // AfterBlock is null when the loop has no clean single exit.
    private sealed class ActiveLoop(NaturalLoop loop, int? afterBlock)
    {
        public NaturalLoop Loop { get; } = loop;
        public int? AfterBlock { get; } = afterBlock;
        public string? Label { get; set; }
    }

    private readonly ControlFlowGraph _cfg;
    private readonly DominatorTree _doms;
    private readonly PostDominatorTree _pdoms;
    private readonly LoopForest _loops;
    private readonly IrLowering _lowering;
    private readonly HashSet<int> _visited = [];

    // Shared break/continue trampoline blocks, keyed so a later path that
    // revisits one re-emits the statement instead of an empty (wrong) block.
    private readonly Dictionary<int, AstStatement> _pureControlBlocks = [];

    private AstBuilder(ControlFlowGraph cfg, DominatorTree doms, PostDominatorTree pdoms, LoopForest loops, IrLowering lowering)
    {
        _cfg = cfg;
        _doms = doms;
        _pdoms = pdoms;
        _loops = loops;
        _lowering = lowering;
    }

    public static AstBlock StructureFunction(
        ControlFlowGraph cfg, DominatorTree doms, PostDominatorTree pdoms, LoopForest loops, IrLowering lowering)
    {
        var builder = new AstBuilder(cfg, doms, pdoms, loops, lowering);
        return builder.Build();
    }

    private AstBlock Build()
    {
        if (_cfg.Blocks.IsEmpty) return new AstBlock([]);

        var main = StructureRegion(_cfg.EntryBlockId, null, []);

        var leftover = _cfg.Blocks.Select(b => b.Id).Where(id => !_visited.Contains(id)).OrderBy(x => x).ToList();
        if (leftover.Count == 0) return main;

        var statements = new List<AstStatement>(main.Statements)
        {
            new AstComment("unreachable via structured control flow")
        };
        statements.AddRange(FlattenBlocks(leftover));
        return new AstBlock([.. statements]);
    }

    // Walks from `entry` until `regionExit`, a dead end, or an already-visited
    // block. `enclosing` holds the loops (innermost first) being structured.
    private AstBlock StructureRegion(int entry, int? regionExit, List<ActiveLoop> enclosing)
    {
        var statements = new List<AstStatement>();
        int? current = entry;

        while (current is int cur && cur != regionExit)
        {
            if (_loops.LoopByHeader.TryGetValue(cur, out var loop) && !_visited.Contains(cur))
            {
                var (loopStmt, after) = StructureLoop(loop, enclosing);
                statements.Add(loopStmt);
                current = after;
                continue;
            }

            if (!_visited.Add(cur))
            {
                if (_pureControlBlocks.TryGetValue(cur, out var cached))
                    statements.Add(cached);
                break;
            }
            current = ProcessBlock(cur, enclosing, statements);
        }

        return new AstBlock([.. statements]);
    }

    // Returns the next block to continue from; null = this path is done.
    private int? ProcessBlock(int cur, List<ActiveLoop> enclosing, List<AstStatement> statements)
    {
        var block = _cfg.Block(cur);
        var body = LowerBody(block);
        statements.AddRange(body);
        var edges = _cfg.Successors(cur);
        var last = block.Instructions[^1];

        if (edges.IsEmpty)
            return null;

        if (IsConditionalBranch(edges))
        {
            var (ifStmt, after) = StructureIfElse(cur, last, edges, enclosing);
            statements.Add(ifStmt);
            return after;
        }

        if (Find(edges, EdgeKind.Exception) is { } exceptionEdge)
        {
            var (tryStmt, after) = StructureTryCatch(cur, last, exceptionEdge, enclosing);
            statements.Add(tryStmt);
            return after;
        }

        if (edges.Any(e => e.Kind is EdgeKind.SwitchCase or EdgeKind.SwitchDefault))
        {
            var (switchStmt, after) = StructureSwitch(cur, last, edges, enclosing);
            statements.Add(switchStmt);
            return after;
        }

        if (edges.Length == 1)
        {
            int before = statements.Count;
            int? next = ResolveSingleSuccessor(edges[0].ToBlock, enclosing, statements);
            if (next is null && body.Count == 0 && statements.Count == before + 1)
                _pureControlBlocks[cur] = statements[^1];
            return next;
        }

        statements.AddRange(FlattenTerminatorEdges(last, edges));
        return Find(edges, EdgeKind.Fallthrough)?.ToBlock;
    }

    private (AstStatement, int?) StructureSwitch(
        int headerBlockId, HlInstruction last, ImmutableArray<CfgEdge> edges, List<ActiveLoop> enclosing)
    {
        var scrutinee = _lowering.SwitchScrutinee(last);
        bool hasMerge = _pdoms.HasPostDominator(headerBlockId);
        int merge = hasMerge ? _pdoms.ImmediatePostDominator(headerBlockId) : PostDominatorTree.VirtualExitId;
        int? afterMerge = merge == PostDominatorTree.VirtualExitId ? null : merge;

        var cases = new List<AstSwitchCase>();
        foreach (var e in edges.Where(e => e.Kind == EdgeKind.SwitchCase).OrderBy(e => e.CaseValue))
            cases.Add(new AstSwitchCase(e.CaseValue, StructureCaseBody(e.ToBlock, afterMerge, enclosing)));

        if (Find(edges, EdgeKind.SwitchDefault) is { } def)
            cases.Add(new AstSwitchCase(null, StructureCaseBody(def.ToBlock, afterMerge, enclosing)));

        return (new AstSwitch(scrutinee, [.. cases]), afterMerge);
    }

    private AstBlock StructureCaseBody(int target, int? afterMerge, List<ActiveLoop> enclosing)
    {
        if (ClassifyLoopTarget(target, enclosing) is { } classified) return new AstBlock([classified]);
        if (target == afterMerge) return EmptyBlock;
        if (!_visited.Contains(target)) return StructureRegion(target, afterMerge, enclosing);
        return _pureControlBlocks.TryGetValue(target, out var cached) ? new AstBlock([cached]) : EmptyBlock;
    }

    // Best-effort: guarded region is Trap's fallthrough up to the first
    // EndTrap before the handler; falls back to a comment if not found.
    private (AstStatement, int?) StructureTryCatch(
        int trapBlockId, HlInstruction trapInstr, CfgEdge exceptionEdge, List<ActiveLoop> enclosing)
    {
        int excReg = trapInstr.Operands[0];
        int handlerStart = exceptionEdge.ToBlock;
        int? endTrapOffset = FindMatchingEndTrap(trapInstr.Offset, _cfg.Block(handlerStart).Start);

        if (endTrapOffset is not int eto)
        {
            var fallback = Find(_cfg.Successors(trapBlockId), EdgeKind.Fallthrough)?.ToBlock;
            return (new AstComment($"try (unrecognized shape, exc reg r{excReg}, handler {LabelName(handlerStart)})"), fallback);
        }

        var endTrapBlock = _cfg.BlockAt(eto);
        int tryFallthrough = Find(_cfg.Successors(trapBlockId), EdgeKind.Fallthrough)!.Value.ToBlock;

        var tryStatements = new List<AstStatement>(StructureRegion(tryFallthrough, endTrapBlock.Id, enclosing).Statements);

        _visited.Add(endTrapBlock.Id);
        int? afterBlock = ProcessBlock(endTrapBlock.Id, enclosing, tryStatements);

        var catchBody = StructureRegion(handlerStart, afterBlock, enclosing);
        var catchStmt = new AstCatch(_lowering.Register(excReg), catchBody);
        return (new AstTry(new AstBlock([.. tryStatements]), catchStmt), afterBlock);
    }

    private int? FindMatchingEndTrap(int trapOffset, int handlerStartOffset)
    {
        foreach (var instr in _cfg.Function.Instructions)
            if (instr.Opcode == HlOpcode.EndTrap && instr.Offset > trapOffset && instr.Offset < handlerStartOffset)
                return instr.Offset;
        return null;
    }

    private static bool IsConditionalBranch(ImmutableArray<CfgEdge> edges) =>
        edges.Length == 2
        && edges.Any(e => e.Kind == EdgeKind.Fallthrough)
        && edges.Any(e => e.Kind == EdgeKind.Jump);

    private (AstStatement, int?) StructureIfElse(
        int headerBlockId, HlInstruction last, ImmutableArray<CfgEdge> edges, List<ActiveLoop> enclosing)
    {
        var cond = _lowering.ConditionFromBranch(last);
        int jumpTarget = Find(edges, EdgeKind.Jump)!.Value.ToBlock;
        int fallTarget = Find(edges, EdgeKind.Fallthrough)!.Value.ToBlock;

        // Checked before merge structuring so loop exits aren't mistaken for an if/else merge.
        var jumpExit = ClassifyLoopTarget(jumpTarget, enclosing);
        var fallExit = ClassifyLoopTarget(fallTarget, enclosing);

        if (jumpExit is not null && fallExit is not null)
            return (new AstIf(cond, new AstBlock([jumpExit]), new AstBlock([fallExit])), null);
        if (jumpExit is not null)
            return (new AstIf(cond, new AstBlock([jumpExit]), null), fallTarget);
        if (fallExit is not null)
            return (new AstIf(new UnaryExpr(UnaryOp.Not, cond), new AstBlock([fallExit]), null), jumpTarget);

        bool hasMerge = _pdoms.HasPostDominator(headerBlockId);
        int merge = hasMerge ? _pdoms.ImmediatePostDominator(headerBlockId) : 0;
        bool noRealMerge = !hasMerge || merge == PostDominatorTree.VirtualExitId;

        if (noRealMerge)
        {
            // Guard-clause shape: both branches terminate independently.
            var thenBody = StructureRegion(jumpTarget, null, enclosing);
            return (new AstIf(cond, thenBody, null), fallTarget);
        }

        var then = jumpTarget == merge ? EmptyBlock : StructureRegion(jumpTarget, merge, enclosing);
        AstBlock? els = fallTarget == merge ? null : StructureRegion(fallTarget, merge, enclosing);
        return (new AstIf(cond, then, els), merge);
    }

    private (AstStatement, int?) StructureLoop(NaturalLoop loop, List<ActiveLoop> enclosing)
    {
        // The header's post-dominator is where every path leaving the loop reconverges.
        int? afterBlock = LoopContinuation(loop.HeaderBlockId);
        var active = new ActiveLoop(loop, afterBlock);

        _visited.Add(loop.HeaderBlockId);
        enclosing.Insert(0, active);

        var bodyStatements = new List<AstStatement>();
        // ProcessBlock directly, not StructureRegion, to avoid re-recognizing
        // the header as this same loop and recursing forever.
        int? next = ProcessBlock(loop.HeaderBlockId, enclosing, bodyStatements);
        if (next is int n && n != afterBlock)
            bodyStatements.AddRange(StructureRegion(n, afterBlock, enclosing).Statements);

        enclosing.RemoveAt(0);

        return (new AstWhile(new BoolLiteral(true), new AstBlock([.. bodyStatements]), active.Label), afterBlock);
    }

    // Target is an enclosing loop's header (continue) or exit point (break), if any.
    private AstStatement? ClassifyLoopTarget(int target, List<ActiveLoop> enclosing)
    {
        for (int i = 0; i < enclosing.Count; i++)
        {
            var active = enclosing[i];
            if (target == active.AfterBlock) return new AstBreak(i == 0 ? null : EnsureLabel(active));
            if (target == active.Loop.HeaderBlockId) return new AstContinue(i == 0 ? null : EnsureLabel(active));
        }
        return null;
    }

    private int? ResolveSingleSuccessor(int target, List<ActiveLoop> enclosing, List<AstStatement> statements)
    {
        var classified = ClassifyLoopTarget(target, enclosing);
        if (classified is not null)
        {
            statements.Add(classified);
            return null;
        }
        return target;
    }

    private static string EnsureLabel(ActiveLoop active) => active.Label ??= $"loop{active.Loop.HeaderBlockId}";

    // Offset order matters: fallthrough edges rely on it to stay implicit.
    private List<AstStatement> FlattenBlocks(IEnumerable<int> blockIds)
    {
        var ids = blockIds.ToList();
        foreach (int id in ids) _visited.Add(id);

        var needsLabel = new HashSet<int>();
        foreach (int id in ids)
            foreach (var e in _cfg.Successors(id))
                if (e.Kind is EdgeKind.Jump or EdgeKind.SwitchCase or EdgeKind.SwitchDefault)
                    needsLabel.Add(e.ToBlock);

        var statements = new List<AstStatement>();
        foreach (int id in ids)
        {
            if (needsLabel.Contains(id))
                statements.Add(new AstLabel(LabelName(id)));

            var block = _cfg.Block(id);
            statements.AddRange(LowerBody(block));
            var last = block.Instructions[^1];
            statements.AddRange(FlattenTerminatorEdges(last, _cfg.Successors(id)));
        }
        return statements;
    }

    private List<AstStatement> FlattenTerminatorEdges(HlInstruction last, ImmutableArray<CfgEdge> edges)
    {
        var statements = new List<AstStatement>();
        if (edges.IsEmpty) return statements; // Ret/Throw already emitted

        var switchEdges = edges.Where(e => e.Kind is EdgeKind.SwitchCase or EdgeKind.SwitchDefault).ToList();
        if (switchEdges.Count > 0)
        {
            var scrutinee = _lowering.SwitchScrutinee(last);
            foreach (var e in switchEdges.Where(e => e.Kind == EdgeKind.SwitchCase).OrderBy(e => e.CaseValue))
            {
                var cmp = new BinaryExpr(BinaryOp.Eq, scrutinee, new IntLiteral(e.CaseValue!.Value));
                statements.Add(new AstIf(cmp, new AstBlock([new AstGoto(LabelName(e.ToBlock))]), null));
            }
            var def = switchEdges.FirstOrDefault(e => e.Kind == EdgeKind.SwitchDefault);
            if (def.Kind == EdgeKind.SwitchDefault)
                statements.Add(new AstGoto(LabelName(def.ToBlock)));
            return statements;
        }

        if (Find(edges, EdgeKind.Exception) is { } exception)
            statements.Add(new AstComment($"trap -> handler {LabelName(exception.ToBlock)}"));

        if (Find(edges, EdgeKind.Jump) is { } jump)
        {
            statements.Add(Find(edges, EdgeKind.Fallthrough) is not null
                ? new AstIf(_lowering.ConditionFromBranch(last), new AstBlock([new AstGoto(LabelName(jump.ToBlock))]), null)
                : new AstGoto(LabelName(jump.ToBlock)));
        }

        return statements;
    }

    private List<AstStatement> LowerBody(BasicBlock block)
    {
        var statements = new List<AstStatement>();
        foreach (var instr in block.Instructions)
        {
            var stmt = _lowering.Lower(instr);
            if (stmt is not null) statements.Add(new AstLeaf(stmt));
        }
        return statements;
    }

    private int? LoopContinuation(int headerBlockId)
    {
        if (!_pdoms.HasPostDominator(headerBlockId)) return null;
        int pd = _pdoms.ImmediatePostDominator(headerBlockId);
        return pd == PostDominatorTree.VirtualExitId ? null : pd;
    }

    private string LabelName(int blockId) => $"L{_cfg.Block(blockId).Start:D4}";

    private static CfgEdge? Find(ImmutableArray<CfgEdge> edges, EdgeKind kind) =>
        edges.Where(e => e.Kind == kind).Select(e => (CfgEdge?)e).FirstOrDefault();

    private static readonly AstBlock EmptyBlock = new([]);
}
