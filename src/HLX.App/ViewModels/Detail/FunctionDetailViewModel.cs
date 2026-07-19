namespace HLX.App.ViewModels.Detail;

public sealed partial class FunctionDetailViewModel : ObservableObject
{
    public string FunctionName { get; }
    public string Signature { get; }
    public int FunctionFIndex { get; }
    public string? SourceFile { get; }
    public IReadOnlyList<RegisterViewModel> Registers { get; }
    public IReadOnlyList<InstructionLineViewModel> Instructions { get; }
    public IReadOnlyList<CallerItem> Callers { get; }

    [ObservableProperty] private bool _showPseudocode;

    private readonly HlFunction _fn;
    private readonly HlModule _module;
    private readonly IReadOnlyDictionary<int, string> _funcNames;
    private IReadOnlyList<PseudoCodeLine>? _pseudoLines;
    public IReadOnlyList<PseudoCodeLine> PseudoLines =>
        _pseudoLines ??= GenerateStructuredPseudocode(_fn, _module, _funcNames);

    private static IReadOnlyList<PseudoCodeLine> GenerateStructuredPseudocode(
        HlFunction fn, HlModule module, IReadOnlyDictionary<int, string> funcNames)
    {
        if (fn.Instructions.IsEmpty) return [];

        var cfg = GraphBuilder.Build(fn);
        var doms = DominatorTree.Compute(cfg);
        var pdoms = PostDominatorTree.Compute(cfg);
        var loops = LoopForest.Build(cfg, doms);
        var lowering = new IrLowering(fn, module, funcNames);
        var ast = AstBuilder.StructureFunction(cfg, doms, pdoms, loops, lowering);

        return HaxePrinter.Print(ast)
            .Select(l => new PseudoCodeLine(l.Text, MapKind(l.Kind)))
            .ToList();
    }

    private static PseudoLineKind MapKind(PrintedLineKind kind) => kind switch
    {
        PrintedLineKind.Label => PseudoLineKind.Label,
        PrintedLineKind.Comment => PseudoLineKind.Comment,
        _ => PseudoLineKind.Code
    };

    private FunctionDetailViewModel(
        string name,
        string signature,
        int findex,
        string? sourceFile,
        IReadOnlyList<RegisterViewModel> registers,
        IReadOnlyList<InstructionLineViewModel> instructions,
        IReadOnlyList<CallerItem> callers,
        HlFunction fn,
        HlModule module,
        IReadOnlyDictionary<int, string> funcNames)
    {
        FunctionName = name;
        Signature = signature;
        FunctionFIndex = findex;
        SourceFile = sourceFile;
        Registers = registers;
        Instructions = instructions;
        Callers = callers;
        _fn = fn;
        _module = module;
        _funcNames = funcNames;
    }

    public static FunctionDetailViewModel Create(
        int findex,
        HlModule module,
        AnalysisResult analysis,
        IReadOnlyDictionary<int, string> funcNames,
        INavigationService nav)
    {
        HlFunction? fn = null;
        foreach (var f in module.Functions)
            if (f.FunctionIndex == findex) { fn = f; break; }
        if (fn == null)
            return new FunctionDetailViewModel($"fn#{findex}", "?", findex, null, [], [], [],
                new HlFunction(new FunctionType([], 0), findex, [], [], []), module, funcNames);
        var resolver = analysis.TypeNames;

        string name = funcNames.TryGetValue(findex, out var n) ? n : $"fn#{findex}";
        var args = string.Join(", ", fn.Type.ArgTypes.Select(resolver.Resolve));
        string signature = $"({args}) -> {resolver.Resolve(fn.Type.ReturnType)}";

        string? sourceFile = null;
        if (fn.DebugInfo.Length > 0)
        {
            var d = fn.DebugInfo[0];
            if ((uint)d.FileIndex < (uint)module.DebugFiles.Length)
                sourceFile = $"{module.DebugFiles[d.FileIndex]}:{d.Line}";
        }

        var registers = fn.Registers
            .Select((t, i) => new RegisterViewModel(i, t, resolver, module))
            .ToList();

        var instructions = BuildInstructions(fn, module, resolver, nav);

        var callerFindices = analysis.CallGraph.Callers(findex);
        var callers = callerFindices
            .Select(callerFi =>
            {
                string callerName = funcNames.TryGetValue(callerFi, out var cn) ? cn : $"fn#{callerFi}";
                return new CallerItem(callerName, callerFi, nav.NavigateToFunction);
            })
            .ToList();

        return new FunctionDetailViewModel(name, signature, findex, sourceFile, registers, instructions, callers,
            fn, module, funcNames);
    }

    private static IReadOnlyList<InstructionLineViewModel> BuildInstructions(
        HlFunction fn, HlModule module, TypeNameResolver resolver, INavigationService nav)
    {
        if (fn.Instructions.IsEmpty) return [];

        var jumpTargets = new HashSet<int>();
        foreach (var instr in fn.Instructions)
        {
            var kinds = HlOpcodeInfo.Operands(instr.Opcode);
            var ops = instr.Operands;
            int oi = 0;
            bool done = false;
            for (int ki = 0; ki < kinds.Length && oi < ops.Length && !done; ki++)
            {
                switch (kinds[ki])
                {
                    case HlOperandKind.JumpOffset:
                        jumpTargets.Add(instr.Offset + 1 + ops[oi++]);
                        break;
                    case HlOperandKind.SwitchTable:
                    {
                        int cnt = ops[oi++];
                        for (int j = 0; j < cnt && oi < ops.Length; j++)
                            jumpTargets.Add(instr.Offset + 1 + ops[oi++]);
                        if (oi < ops.Length)
                            jumpTargets.Add(instr.Offset + 1 + ops[oi++]);
                        done = true;
                        break;
                    }
                    case HlOperandKind.CallArgs:
                        done = true;
                        break;
                    default:
                        oi++;
                        break;
                }
            }
        }

        int pad = fn.Instructions[^1].Offset.ToString().Length;
        var lines = new List<InstructionLineViewModel>(fn.Instructions.Length);

        foreach (var instr in fn.Instructions)
        {
            string? label = jumpTargets.Contains(instr.Offset) ? $"L{instr.Offset:D4}:" : null;
            string offset = instr.Offset.ToString().PadLeft(pad) + ": ";
            string mnemonic = HlOpcodeInfo.Name(instr.Opcode).PadRight(14) + " ";

            string? debugInfo = null;
            if ((uint)instr.Offset < (uint)fn.DebugInfo.Length)
            {
                var d = fn.DebugInfo[instr.Offset];
                if ((uint)d.FileIndex < (uint)module.DebugFiles.Length)
                    debugInfo = $"  ; {module.DebugFiles[d.FileIndex]}:{d.Line}";
            }

            var parts = BuildParts(instr, fn, module, resolver, nav, jumpTargets);
            lines.Add(new InstructionLineViewModel(label, offset, mnemonic, parts, debugInfo));
        }

        return lines;
    }

    private static IReadOnlyList<InstructionPartViewModel> BuildParts(
        HlInstruction instr, HlFunction fn, HlModule module,
        TypeNameResolver resolver, INavigationService nav,
        HashSet<int> jumpTargets)
    {
        var kinds = HlOpcodeInfo.Operands(instr.Opcode);
        if (kinds.IsEmpty) return [];

        var parts = new List<InstructionPartViewModel>();
        var ops = instr.Operands;
        int oi = 0;

        for (int ki = 0; ki < kinds.Length && oi < ops.Length; ki++)
        {
            if (ki > 0 && kinds[ki] != HlOperandKind.CallArgs && kinds[ki] != HlOperandKind.SwitchTable)
                parts.Add(new TextPartViewModel(", "));

            switch (kinds[ki])
            {
                case HlOperandKind.Register:
                {
                    int ri = ops[oi++];
                    string regType = (uint)ri < (uint)fn.Registers.Length
                        ? FormatRegTypeName(fn.Registers[ri])
                        : "?";
                    parts.Add(new TextPartViewModel($"r{ri}:{regType}"));
                    break;
                }
                case HlOperandKind.IntConst:
                {
                    int idx = ops[oi++];
                    string v = (uint)idx < (uint)module.Ints.Length ? module.Ints[idx].ToString() : $"int[{idx}]";
                    parts.Add(new TextPartViewModel(v));
                    break;
                }
                case HlOperandKind.FloatConst:
                {
                    int idx = ops[oi++];
                    string v = (uint)idx < (uint)module.Floats.Length ? module.Floats[idx].ToString("G") : $"float[{idx}]";
                    parts.Add(new TextPartViewModel(v));
                    break;
                }
                case HlOperandKind.StringConst:
                {
                    int idx = ops[oi++];
                    string full = (uint)idx < (uint)module.Strings.Length ? module.Strings[idx] : "?";
                    string display = '"' + Truncate(Escape(full), 32) + '"';
                    parts.Add(new StringLinkViewModel(display, full, nav.ShowString));
                    break;
                }
                case HlOperandKind.BytesConst:
                    parts.Add(new TextPartViewModel($"bytes[{ops[oi++]}]"));
                    break;

                case HlOperandKind.TypeIndex:
                {
                    int ti = ops[oi++];
                    if ((uint)ti < (uint)module.Types.Length)
                        parts.Add(new TypeLinkViewModel(resolver.Resolve(ti), ti, nav.NavigateToType));
                    else
                        parts.Add(new TextPartViewModel($"type[{ti}]"));
                    break;
                }
                case HlOperandKind.GlobalIndex:
                {
                    int gi = ops[oi++];
                    string t = "";
                    if ((uint)gi < (uint)module.Globals.Length)
                    {
                        int ti = module.Globals[gi];
                        if ((uint)ti < (uint)module.Types.Length)
                            t = ":" + resolver.Resolve(ti);
                    }
                    parts.Add(new TextPartViewModel($"global[{gi}]{t}"));
                    break;
                }
                case HlOperandKind.FunctionRef:
                {
                    int fi = ops[oi++];
                    bool isNative = fi < module.Natives.Length;
                    string fnName = isNative
                        ? (module.Natives.FirstOrDefault(n => n.FunctionIndex == fi) is { } nat ? $"{nat.Lib}.{nat.Name}" : $"fn#{fi}")
                        : $"fn#{fi}";
                    if (!isNative)
                        parts.Add(new FuncLinkViewModel(fnName, fi, nav.NavigateToFunction));
                    else
                        parts.Add(new TextPartViewModel(fnName));
                    break;
                }
                case HlOperandKind.FieldIndex:
                    parts.Add(new TextPartViewModel($"field[{ops[oi++]}]"));
                    break;

                case HlOperandKind.JumpOffset:
                {
                    int delta = ops[oi++];
                    int target = instr.Offset + 1 + delta;
                    parts.Add(new TextPartViewModel($"@L{target:D4}"));
                    break;
                }
                case HlOperandKind.SwitchTable:
                {
                    if (ki > 0) parts.Add(new TextPartViewModel(", "));
                    int cnt = ops[oi++];
                    parts.Add(new TextPartViewModel("{"));
                    for (int j = 0; j < cnt && oi < ops.Length; j++)
                    {
                        if (j > 0) parts.Add(new TextPartViewModel(", "));
                        int target = instr.Offset + 1 + ops[oi++];
                        parts.Add(new TextPartViewModel($"@L{target:D4}"));
                    }
                    if (oi < ops.Length)
                    {
                        int def = instr.Offset + 1 + ops[oi++];
                        parts.Add(new TextPartViewModel($"; default:@L{def:D4}}}"));
                    }
                    else
                    {
                        parts.Add(new TextPartViewModel("}"));
                    }
                    goto done;
                }
                case HlOperandKind.CallArgs:
                {
                    int cnt = ops[oi++];
                    parts.Add(new TextPartViewModel("("));
                    for (int j = 0; j < cnt && oi < ops.Length; j++)
                    {
                        if (j > 0) parts.Add(new TextPartViewModel(", "));
                        int ri = ops[oi++];
                        string rt = (uint)ri < (uint)fn.Registers.Length ? FormatRegTypeName(fn.Registers[ri]) : "?";
                        parts.Add(new TextPartViewModel($"r{ri}:{rt}"));
                    }
                    parts.Add(new TextPartViewModel(")"));
                    goto done;
                }
                case HlOperandKind.Inline:
                    parts.Add(new TextPartViewModel(ops[oi++].ToString()));
                    break;
            }
        }
        done:
        return parts;
    }

    private static string FormatRegTypeName(HlType type) => type switch
    {
        PrimitiveType p => p.Kind.ToString().ToLowerInvariant(),
        ObjectType o => o.Name,
        AbstractType a => a.Name,
        EnumType e => e.Name,
        VirtualType => "virtual",
        FunctionType => "fun",
        ReferenceType r => r.Kind.ToString().ToLowerInvariant(),
        _ => "?"
    };

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static string Truncate(string s, int max) =>
        s.Length > max ? s[..max] + "…" : s;
}
