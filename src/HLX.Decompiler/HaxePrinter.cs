namespace HLX.Decompiler;

public enum PrintedLineKind { Label, Code, Comment }

public sealed record PrintedLine(string Text, PrintedLineKind Kind);

// Pure text formatting over the AST; control-flow decisions are AstBuilder's job.
public static class HaxePrinter
{
    public static IReadOnlyList<PrintedLine> Print(AstBlock root)
    {
        var sink = new List<PrintedLine>();
        PrintBlock(root, 0, sink);
        return sink;
    }

    private static void PrintBlock(AstBlock block, int depth, List<PrintedLine> sink)
    {
        foreach (var stmt in block.Statements)
            PrintStmt(stmt, depth, sink);
    }

    private static void PrintStmt(AstStatement stmt, int depth, List<PrintedLine> sink)
    {
        switch (stmt)
        {
            case AstBlock b:
                PrintBlock(b, depth, sink);
                break;

            case AstLeaf leaf:
                PrintIr(leaf.Ir, depth, sink);
                break;

            case AstIf(var cond, var then, var els):
                sink.Add(new PrintedLine(Indent(depth) + $"if ({RenderExpr(cond)}) {{", PrintedLineKind.Code));
                PrintBlock(then, depth + 1, sink);
                if (els is not null)
                {
                    sink.Add(new PrintedLine(Indent(depth) + "} else {", PrintedLineKind.Code));
                    PrintBlock(els, depth + 1, sink);
                }
                sink.Add(new PrintedLine(Indent(depth) + "}", PrintedLineKind.Code));
                break;

            case AstWhile(var cond, var body, var label):
                string prefix = label is null ? "" : $"{label}: ";
                sink.Add(new PrintedLine(Indent(depth) + prefix + $"while ({RenderExpr(cond)}) {{", PrintedLineKind.Code));
                PrintBlock(body, depth + 1, sink);
                sink.Add(new PrintedLine(Indent(depth) + "}", PrintedLineKind.Code));
                break;

            case AstSwitch(var scrutinee, var cases):
                sink.Add(new PrintedLine(Indent(depth) + $"switch ({RenderExpr(scrutinee)}) {{", PrintedLineKind.Code));
                foreach (var c in cases)
                {
                    string caseLine = c.Value is int v ? $"case {v}:" : "default:";
                    sink.Add(new PrintedLine(Indent(depth + 1) + caseLine, PrintedLineKind.Code));
                    PrintBlock(c.Body, depth + 2, sink);
                }
                sink.Add(new PrintedLine(Indent(depth) + "}", PrintedLineKind.Code));
                break;

            case AstBreak(var label):
                sink.Add(new PrintedLine(Indent(depth) + (label is null ? "break;" : $"break {label};"), PrintedLineKind.Code));
                break;

            case AstContinue(var label):
                sink.Add(new PrintedLine(Indent(depth) + (label is null ? "continue;" : $"continue {label};"), PrintedLineKind.Code));
                break;

            case AstGoto(var label):
                sink.Add(new PrintedLine(Indent(depth) + $"goto {label};", PrintedLineKind.Code));
                break;

            case AstLabel(var label):
                sink.Add(new PrintedLine($"{label}:", PrintedLineKind.Label));
                break;

            case AstTry(var tryBody, var @catch):
                sink.Add(new PrintedLine(Indent(depth) + "try {", PrintedLineKind.Code));
                PrintBlock(tryBody, depth + 1, sink);
                sink.Add(new PrintedLine(Indent(depth) + $"}} catch ({RenderExpr(@catch.ExceptionRegister)}) {{", PrintedLineKind.Code));
                PrintBlock(@catch.Body, depth + 1, sink);
                sink.Add(new PrintedLine(Indent(depth) + "}", PrintedLineKind.Code));
                break;

            case AstComment(var text):
                sink.Add(new PrintedLine(Indent(depth) + "// " + text, PrintedLineKind.Comment));
                break;
        }
    }

    private static void PrintIr(IrStatement ir, int depth, List<PrintedLine> sink)
    {
        if (ir is CommentStatement cs)
        {
            sink.Add(new PrintedLine(Indent(depth) + "// " + cs.Text, PrintedLineKind.Comment));
            return;
        }

        string text = ir switch
        {
            AssignStatement s => $"{RenderExpr(s.Target)} = {RenderExpr(s.Value)};",
            ExprStatement s => $"{RenderExpr(s.Expr)};",
            FieldAssignStatement s => $"{RenderExpr(s.Target)}.{s.FieldName} = {RenderExpr(s.Value)};",
            ThisFieldAssignStatement s => $"this.{s.FieldName} = {RenderExpr(s.Value)};",
            DynFieldAssignStatement s => $"{RenderExpr(s.Target)}.{s.FieldName} = {RenderExpr(s.Value)};",
            GlobalAssignStatement s => $"@global[{s.GlobalIndex}] = {RenderExpr(s.Value)};",
            MemorySetStatement s => $"{RenderExpr(s.Target)}[{RenderExpr(s.Index)}] = {RenderExpr(s.Value)};{MemComment(s.Kind)}",
            IncrStatement s => $"{RenderExpr(s.Target)}{(s.IsIncrement ? "++" : "--")};",
            SetEnumFieldStatement s => $"{RenderExpr(s.Target)}.field[{s.FieldSlot}] = {RenderExpr(s.Value)};",
            SetRefStatement s => $"{RenderExpr(s.Target)}.set({RenderExpr(s.Value)});",
            ReturnStatement { Value: null } => "return;",
            ReturnStatement s => $"return {RenderExpr(s.Value!)};",
            ThrowStatement s => $"throw {RenderExpr(s.Value)};{(s.IsRethrow ? "  // rethrow" : "")}",
            _ => $"// {ir.GetType().Name}"
        };
        sink.Add(new PrintedLine(Indent(depth) + text, PrintedLineKind.Code));
    }

    private static string RenderExpr(IrExpr expr) => expr switch
    {
        RegisterExpr r => $"r{r.Register}",
        IntLiteral i => i.Value.ToString(),
        FloatLiteral f => f.Value.ToString("G"),
        BoolLiteral b => b.Value ? "true" : "false",
        StringLiteral s => $"\"{Escape(s.Value)}\"",
        NullLiteral => "null",
        BytesLiteral b => $"bytes[{b.BytesIndex}]",
        BinaryExpr be => $"{RenderExpr(be.Left)} {BinaryOpText(be.Op)} {RenderExpr(be.Right)}{UnsignedSuffix(be.Op)}",
        UnaryExpr { Op: UnaryOp.Neg } ue => $"-{RenderExpr(ue.Operand)}",
        UnaryExpr ue => $"!{RenderExpr(ue.Operand)}",
        FieldAccessExpr fa => $"{RenderExpr(fa.Target)}.{fa.FieldName}",
        ThisFieldAccessExpr tf => $"this.{tf.FieldName}",
        DynFieldAccessExpr df => $"{RenderExpr(df.Target)}.{df.FieldName}",
        GlobalAccessExpr g => $"@global[{g.GlobalIndex}]",
        MemoryAccessExpr m => $"{RenderExpr(m.Target)}[{RenderExpr(m.Index)}]{MemComment(m.Kind)}",
        CallExpr c => $"{RenderCallee(c.Callee)}({string.Join(", ", c.Args.Select(RenderExpr))})",
        ThisExpr => "this",
        FuncRefExpr f => f.DisplayName,
        NewObjectExpr n => $"new {TypeNaming.ShortTypeName(n.Type)}()",
        CastExpr { Kind: CastKind.ToDyn } c => $"(Dynamic){RenderExpr(c.Operand)}",
        CastExpr { Kind: CastKind.ToSFloat } c => $"(Float){RenderExpr(c.Operand)}",
        CastExpr { Kind: CastKind.ToUFloat } c => $"(Float){RenderExpr(c.Operand)}  // unsigned",
        CastExpr { Kind: CastKind.ToInt } c => $"Std.int({RenderExpr(c.Operand)})",
        CastExpr { Kind: CastKind.SafeCast } c => $"cast({RenderExpr(c.Operand)}, {(c.TargetType is not null ? TypeNaming.ShortTypeName(c.TargetType) : "?")})",
        CastExpr { Kind: CastKind.UnsafeCast } c => $"cast {RenderExpr(c.Operand)}",
        CastExpr { Kind: CastKind.ToVirtual } c => $"cast({RenderExpr(c.Operand)}, Virtual)",
        CastExpr c => RenderExpr(c.Operand),
        TypeValueExpr t => TypeNaming.ShortTypeName(t.Type),
        TypeOfExpr t => $"Type.typeof({RenderExpr(t.Operand)})",
        ArrayLengthExpr a => $"{RenderExpr(a.Operand)}.length",
        RefExpr r => $"new Ref({RenderExpr(r.Operand)})",
        DerefExpr d => $"{RenderExpr(d.Operand)}.get()",
        EnumAllocExpr e => $"new {TypeNaming.ShortTypeName(e.EnumType)}()",
        EnumIndexExpr e => $"{RenderExpr(e.Operand)}.getIndex()",
        EnumFieldExpr e => $"({RenderExpr(e.Operand)} as {e.CtorName}).{e.FieldName}",
        MakeEnumExpr m => $"{m.CtorName}({string.Join(", ", m.Args.Select(RenderExpr))})",
        RawExpr r => r.Text,
        _ => "?"
    };

    private static string RenderCallee(CalleeRef callee) => callee switch
    {
        StaticFuncRef s => s.DisplayName,
        MethodRef m => $"{RenderExpr(m.Receiver)}.{m.DisplayName}",
        ClosureCallRef c => RenderExpr(c.ClosureExpr),
        _ => "?"
    };

    private static string BinaryOpText(BinaryOp op) => op switch
    {
        BinaryOp.Add => "+", BinaryOp.Sub => "-", BinaryOp.Mul => "*",
        BinaryOp.SDiv or BinaryOp.UDiv => "/",
        BinaryOp.SMod or BinaryOp.UMod => "%",
        BinaryOp.Shl => "<<", BinaryOp.SShr => ">>", BinaryOp.UShr => ">>>",
        BinaryOp.And => "&", BinaryOp.Or => "|", BinaryOp.Xor => "^",
        BinaryOp.Lt or BinaryOp.ULt => "<", BinaryOp.Lte => "<=",
        BinaryOp.Gt => ">", BinaryOp.Gte or BinaryOp.UGte => ">=",
        BinaryOp.Eq => "==", BinaryOp.NotEq => "!=",
        _ => "?"
    };

    private static string UnsignedSuffix(BinaryOp op) =>
        op is BinaryOp.UDiv or BinaryOp.UMod or BinaryOp.UShr or BinaryOp.ULt or BinaryOp.UGte ? "  // unsigned" : "";

    private static string MemComment(MemoryKind k) => k switch
    {
        MemoryKind.I8 => "  // i8",
        MemoryKind.I16 => "  // i16",
        _ => ""
    };

    private static string Indent(int depth) => new(' ', depth * 4);

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
