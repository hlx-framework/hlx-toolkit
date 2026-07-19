namespace HLX.Decompiler;

public abstract record AstStatement;

public sealed record AstBlock(ImmutableArray<AstStatement> Statements) : AstStatement;
public sealed record AstLeaf(IrStatement Ir) : AstStatement;
public sealed record AstIf(IrExpr Condition, AstBlock Then, AstBlock? Else) : AstStatement;
public sealed record AstWhile(IrExpr Condition, AstBlock Body, string? Label) : AstStatement;
public sealed record AstSwitchCase(int? Value, AstBlock Body);   // null Value = default
public sealed record AstSwitch(IrExpr Scrutinee, ImmutableArray<AstSwitchCase> Cases) : AstStatement;
public sealed record AstBreak(string? Label) : AstStatement;
public sealed record AstContinue(string? Label) : AstStatement;
public sealed record AstGoto(string Label) : AstStatement;      // fallback path only
public sealed record AstLabel(string Label) : AstStatement;     // fallback path only
public sealed record AstCatch(RegisterExpr ExceptionRegister, AstBlock Body);
public sealed record AstTry(AstBlock TryBody, AstCatch Catch) : AstStatement;   // best-effort
public sealed record AstComment(string Text) : AstStatement;
