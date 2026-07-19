namespace HLX.Decompiler;

public abstract record IrExpr;

public sealed record RegisterExpr(int Register, HlType Type) : IrExpr;
public sealed record IntLiteral(int Value) : IrExpr;
public sealed record FloatLiteral(double Value) : IrExpr;
public sealed record BoolLiteral(bool Value) : IrExpr;
public sealed record StringLiteral(string Value) : IrExpr;
public sealed record NullLiteral : IrExpr;
public sealed record BytesLiteral(int BytesIndex) : IrExpr;

public enum BinaryOp { Add, Sub, Mul, SDiv, UDiv, SMod, UMod, Shl, SShr, UShr, And, Or, Xor,
                        Lt, Lte, Gt, Gte, ULt, UGte, Eq, NotEq }
public sealed record BinaryExpr(BinaryOp Op, IrExpr Left, IrExpr Right) : IrExpr;

public enum UnaryOp { Neg, Not }
public sealed record UnaryExpr(UnaryOp Op, IrExpr Operand) : IrExpr;

public sealed record FieldAccessExpr(IrExpr Target, string FieldName, int FieldSlot) : IrExpr;
public sealed record ThisFieldAccessExpr(string FieldName, int FieldSlot) : IrExpr;
public sealed record DynFieldAccessExpr(IrExpr Target, string FieldName) : IrExpr;
public sealed record GlobalAccessExpr(int GlobalIndex) : IrExpr;

public enum MemoryKind { I8, I16, Mem, Array }
public sealed record MemoryAccessExpr(IrExpr Target, IrExpr Index, MemoryKind Kind) : IrExpr;

public abstract record CalleeRef;
public sealed record StaticFuncRef(int FIndex, string DisplayName) : CalleeRef;
public sealed record MethodRef(IrExpr Receiver, int ProtoSlot, string DisplayName) : CalleeRef;
public sealed record ClosureCallRef(IrExpr ClosureExpr) : CalleeRef;
public sealed record CallExpr(CalleeRef Callee, ImmutableArray<IrExpr> Args) : IrExpr;

public sealed record ThisExpr(HlType? Type) : IrExpr;
public sealed record FuncRefExpr(int FIndex, string DisplayName) : IrExpr;   // a bare function referenced as a value (StaticClosure)
public sealed record NewObjectExpr(HlType Type) : IrExpr;

public enum CastKind { ToDyn, ToSFloat, ToUFloat, ToInt, SafeCast, UnsafeCast, ToVirtual }
public sealed record CastExpr(CastKind Kind, IrExpr Operand, HlType? TargetType) : IrExpr;

public sealed record TypeValueExpr(HlType Type) : IrExpr;
public sealed record TypeOfExpr(IrExpr Operand) : IrExpr;
public sealed record ArrayLengthExpr(IrExpr Operand) : IrExpr;
public sealed record RefExpr(IrExpr Operand) : IrExpr;
public sealed record DerefExpr(IrExpr Operand) : IrExpr;
public sealed record EnumAllocExpr(HlType EnumType) : IrExpr;
public sealed record EnumIndexExpr(IrExpr Operand) : IrExpr;
public sealed record EnumFieldExpr(IrExpr Operand, string CtorName, string FieldName) : IrExpr;
public sealed record MakeEnumExpr(string CtorName, ImmutableArray<IrExpr> Args) : IrExpr;

// Fallback for rarely-hit or unrecognized opcodes.
public sealed record RawExpr(string Text) : IrExpr;
