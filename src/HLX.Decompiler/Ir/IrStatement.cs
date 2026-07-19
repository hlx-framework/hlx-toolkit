namespace HLX.Decompiler;

public abstract record IrStatement;

public sealed record AssignStatement(RegisterExpr Target, IrExpr Value) : IrStatement;
public sealed record ExprStatement(IrExpr Expr) : IrStatement;
public sealed record FieldAssignStatement(IrExpr Target, string FieldName, int FieldSlot, IrExpr Value) : IrStatement;
public sealed record ThisFieldAssignStatement(string FieldName, int FieldSlot, IrExpr Value) : IrStatement;
public sealed record DynFieldAssignStatement(IrExpr Target, string FieldName, IrExpr Value) : IrStatement;
public sealed record GlobalAssignStatement(int GlobalIndex, IrExpr Value) : IrStatement;
public sealed record MemorySetStatement(IrExpr Target, IrExpr Index, IrExpr Value, MemoryKind Kind) : IrStatement;
public sealed record IncrStatement(RegisterExpr Target, bool IsIncrement) : IrStatement;
public sealed record SetEnumFieldStatement(IrExpr Target, int FieldSlot, IrExpr Value) : IrStatement;
public sealed record SetRefStatement(IrExpr Target, IrExpr Value) : IrStatement;
public sealed record ReturnStatement(IrExpr? Value) : IrStatement;
public sealed record ThrowStatement(IrExpr Value, bool IsRethrow) : IrStatement;
public sealed record CommentStatement(string Text) : IrStatement;
