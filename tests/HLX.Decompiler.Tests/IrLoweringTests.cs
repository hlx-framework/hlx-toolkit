namespace HLX.Decompiler.Tests;

public class IrLoweringTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "hlboot.dat");

    private static HlModule LoadFixture()
    {
        using var fs = File.OpenRead(FixturePath);
        return HlReader.Read(fs);
    }

    // Control-transfer-only opcodes: AstBuilder derives their effect from CFG edges, so Lower() returns null.
    private static readonly HashSet<HlOpcode> ControlTransferOnly =
    [
        HlOpcode.Nop, HlOpcode.Label, HlOpcode.Switch, HlOpcode.Trap, HlOpcode.EndTrap, HlOpcode.Catch,
        HlOpcode.JTrue, HlOpcode.JFalse, HlOpcode.JNull, HlOpcode.JNotNull,
        HlOpcode.JSLt, HlOpcode.JSGte, HlOpcode.JSGt, HlOpcode.JSLte,
        HlOpcode.JULt, HlOpcode.JUGte, HlOpcode.JNotLt, HlOpcode.JNotGte,
        HlOpcode.JEq, HlOpcode.JNotEq, HlOpcode.JAlways,
    ];

    private static readonly HashSet<HlOpcode> ConditionalBranches =
    [
        HlOpcode.JTrue, HlOpcode.JFalse, HlOpcode.JNull, HlOpcode.JNotNull,
        HlOpcode.JSLt, HlOpcode.JSGte, HlOpcode.JSGt, HlOpcode.JSLte,
        HlOpcode.JULt, HlOpcode.JUGte, HlOpcode.JNotLt, HlOpcode.JNotGte,
        HlOpcode.JEq, HlOpcode.JNotEq,
    ];

    private static Dictionary<int, string> BuildFuncNames(HlModule module)
    {
        var names = new Dictionary<int, string>();
        foreach (var type in module.Types)
            if (type is ObjectType obj)
                foreach (var proto in obj.Protos)
                    names.TryAdd(proto.FunctionIndex, $"{obj.Name}::{proto.Name}");
        return names;
    }

    [Fact]
    public void Lower_NeverThrows_AndOnlyNullForControlTransferOpcodes()
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        foreach (var fn in m.Functions)
        {
            var lowering = new IrLowering(fn, m, funcNames);
            foreach (var instr in fn.Instructions)
            {
                var stmt = lowering.Lower(instr);
                if (stmt is null)
                    Assert.Contains(instr.Opcode, ControlTransferOnly);
            }
        }
    }

    [Fact]
    public void ConditionFromBranch_NeverThrowsForConditionalJumps()
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        foreach (var fn in m.Functions)
        {
            var lowering = new IrLowering(fn, m, funcNames);
            foreach (var instr in fn.Instructions)
            {
                if (ConditionalBranches.Contains(instr.Opcode))
                    lowering.ConditionFromBranch(instr);
            }
        }
    }

    [Fact]
    public void Ret_OfVoidRegister_HasNullValue()
    {
        var m = LoadFixture();
        var funcNames = BuildFuncNames(m);
        foreach (var fn in m.Functions)
        {
            var lowering = new IrLowering(fn, m, funcNames);
            foreach (var instr in fn.Instructions)
            {
                if (instr.Opcode != HlOpcode.Ret) continue;
                int reg = instr.Operands[0];
                bool isVoid = (uint)reg < (uint)fn.Registers.Length && fn.Registers[reg] is PrimitiveType { Kind: PrimitiveKind.Void };
                var stmt = Assert.IsType<ReturnStatement>(lowering.Lower(instr));
                Assert.Equal(isVoid, stmt.Value is null);
            }
        }
    }
}
