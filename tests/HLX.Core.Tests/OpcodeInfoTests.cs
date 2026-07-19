using HLX.Core;

namespace HLX.Core.Tests;

public class OpcodeInfoTests
{
    [Fact]
    public void AllOpcodesHaveNonEmptyName()
    {
        for (var op = HlOpcode.Mov; op < HlOpcode.Last; op++)
            Assert.False(string.IsNullOrEmpty(HlOpcodeInfo.Name(op)), $"Missing name for {op}");
    }

    [Fact]
    public void AllOpcodesReturnOperandArray()
    {
        for (var op = HlOpcode.Mov; op < HlOpcode.Last; op++)
            _ = HlOpcodeInfo.Operands(op);
    }

    [Theory]
    [InlineData(HlOpcode.Label,  0)]
    [InlineData(HlOpcode.Assert, 0)]
    [InlineData(HlOpcode.Nop,    0)]
    [InlineData(HlOpcode.Ret,    1)]
    [InlineData(HlOpcode.Throw,  1)]
    [InlineData(HlOpcode.JAlways,1)]
    [InlineData(HlOpcode.Null,   1)]
    [InlineData(HlOpcode.Incr,   1)]
    [InlineData(HlOpcode.Mov,    2)]
    [InlineData(HlOpcode.Int,    2)]
    [InlineData(HlOpcode.Float,  2)]
    [InlineData(HlOpcode.Call0,  2)]
    [InlineData(HlOpcode.JTrue,  2)]
    [InlineData(HlOpcode.JFalse, 2)]
    [InlineData(HlOpcode.Add,    3)]
    [InlineData(HlOpcode.GetI8,  3)]
    [InlineData(HlOpcode.JSLt,   3)]
    [InlineData(HlOpcode.Field,  3)]
    [InlineData(HlOpcode.Call1,  3)]
    [InlineData(HlOpcode.Call2,  4)]
    [InlineData(HlOpcode.Call3,  5)]
    [InlineData(HlOpcode.Call4,  6)]
    // Variable-tail opcodes: descriptor covers fixed head + one tail marker.
    [InlineData(HlOpcode.CallN,       3)]
    [InlineData(HlOpcode.CallMethod,  3)]
    [InlineData(HlOpcode.CallThis,    3)]
    [InlineData(HlOpcode.CallClosure, 3)]
    [InlineData(HlOpcode.MakeEnum,    3)]
    [InlineData(HlOpcode.Switch,      2)]
    [InlineData(HlOpcode.Catch,       1)]
    [InlineData(HlOpcode.Asm,         3)]
    public void OperandDescriptorCountIsExpected(HlOpcode op, int expected)
    {
        Assert.Equal(expected, HlOpcodeInfo.Operands(op).Length);
    }

    [Theory]
    [InlineData(HlOpcode.CallN,       2, HlOperandKind.CallArgs)]
    [InlineData(HlOpcode.CallMethod,  2, HlOperandKind.CallArgs)]
    [InlineData(HlOpcode.CallClosure, 2, HlOperandKind.CallArgs)]
    [InlineData(HlOpcode.MakeEnum,    2, HlOperandKind.CallArgs)]
    [InlineData(HlOpcode.Switch,      1, HlOperandKind.SwitchTable)]
    public void VariableTailKindIsCorrect(HlOpcode op, int tailIndex, HlOperandKind expected)
    {
        Assert.Equal(expected, HlOpcodeInfo.Operands(op)[tailIndex]);
    }

    [Theory]
    [InlineData(HlOpcode.Int,       1, HlOperandKind.IntConst)]
    [InlineData(HlOpcode.Float,     1, HlOperandKind.FloatConst)]
    [InlineData(HlOpcode.String,    1, HlOperandKind.StringConst)]
    [InlineData(HlOpcode.Bytes,     1, HlOperandKind.BytesConst)]
    [InlineData(HlOpcode.Type,      1, HlOperandKind.TypeIndex)]
    [InlineData(HlOpcode.GetGlobal, 1, HlOperandKind.GlobalIndex)]
    [InlineData(HlOpcode.Call0,     1, HlOperandKind.FunctionRef)]
    [InlineData(HlOpcode.Field,     2, HlOperandKind.FieldIndex)]
    [InlineData(HlOpcode.JTrue,     1, HlOperandKind.JumpOffset)]
    [InlineData(HlOpcode.JSLt,      2, HlOperandKind.JumpOffset)]
    [InlineData(HlOpcode.Bool,      1, HlOperandKind.Inline)]
    [InlineData(HlOpcode.Catch,     0, HlOperandKind.JumpOffset)]
    [InlineData(HlOpcode.Asm,       0, HlOperandKind.Inline)]
    public void SpecificOperandKindIsCorrect(HlOpcode op, int index, HlOperandKind expected)
    {
        Assert.Equal(expected, HlOpcodeInfo.Operands(op)[index]);
    }
}
