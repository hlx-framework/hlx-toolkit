namespace HLX.Core;

public enum HlOperandKind
{
    Register,
    IntConst,
    FloatConst,
    StringConst,
    BytesConst,
    TypeIndex,
    GlobalIndex,
    FunctionRef,    // findex: unified index over [Natives..., Functions...]
    FieldIndex,     // field/proto slot within an object or enum type
    JumpOffset,     // signed, relative to the instruction
    SwitchTable,    // variable tail: nOffsets, offsets..., default offset
    CallArgs,       // variable tail: nArgs, arg registers...
    Inline,         // raw immediate: bool literal, construct/field index, mode byte, ...
}
