namespace HLX.Core;

public enum PrimitiveKind
{
    Void   = 0,
    U8     = 1,
    U16    = 2,
    I32    = 3,
    I64    = 4,
    F32    = 5,
    F64    = 6,
    Bool   = 7,
    Bytes  = 8,
    Dyn    = 9,
    Array  = 12,
    Type   = 13,
    DynObj = 16,
    Guid   = 23,
}

// ref<T> (code 14), null<T> (code 19), packed<T> (code 22).
public enum ReferenceKind { Ref, Null, Packed }

public abstract record HlType;

public sealed record PrimitiveType(PrimitiveKind Kind) : HlType;

// Covers both `fun` (code 10) and `method` (code 20).
public sealed record FunctionType(
    ImmutableArray<int> ArgTypes,
    int ReturnType,
    bool IsMethod = false
) : HlType;

// Covers both `obj` (code 11) and `struct` (code 21).
public sealed record ObjectType(
    string Name,
    int? SuperIndex,                    // null = no super
    int GlobalValue,                    // 0 = none
    ImmutableArray<HlField> Fields,
    ImmutableArray<HlProto> Protos,
    ImmutableArray<HlBinding> Bindings,
    bool IsStruct = false
) : HlType;

public sealed record VirtualType(ImmutableArray<HlField> Fields) : HlType;

public sealed record EnumType(
    string Name,
    int GlobalValue,
    ImmutableArray<HlEnumConstruct> Constructs
) : HlType;

public sealed record AbstractType(string Name) : HlType;

public sealed record ReferenceType(ReferenceKind Kind, int InnerTypeIndex) : HlType;

public sealed record HlField(string Name, int TypeIndex);

// FunctionIndex is in the unified findex space; PrototypeIndex is the
// overridden slot in the parent's proto table, -1 if none.
public sealed record HlProto(string Name, int FunctionIndex, int PrototypeIndex);

public sealed record HlBinding(int FieldIndex, int FunctionIndex);

public sealed record HlEnumConstruct(string Name, ImmutableArray<int> ParamTypes);
