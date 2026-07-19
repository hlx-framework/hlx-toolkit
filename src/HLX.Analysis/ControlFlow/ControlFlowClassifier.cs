namespace HLX.Analysis;

public readonly record struct InstructionShape(
    bool FallsThrough,
    ImmutableArray<(int Target, EdgeKind Kind, int? CaseValue)> Successors
);

// Only a few opcodes need explicit handling; the rest fall out of HlOpcodeInfo's
// operand kinds (a JumpOffset operand means a conditional branch).
public static class ControlFlowClassifier
{
    public static InstructionShape Classify(HlInstruction instr)
    {
        var ops = instr.Operands;

        switch (instr.Opcode)
        {
            case HlOpcode.JAlways:
                return new InstructionShape(false,
                    [(Target(instr, ops[0]), EdgeKind.Jump, null)]);

            case HlOpcode.Ret:
            case HlOpcode.Throw:
            case HlOpcode.Rethrow:
                return new InstructionShape(false, []);

            case HlOpcode.Switch:
            {
                if (ops.Length < 2) return new InstructionShape(false, []);
                var builder = ImmutableArray.CreateBuilder<(int, EdgeKind, int?)>();
                int cnt = ops[1], oi = 2;
                for (int j = 0; j < cnt && oi < ops.Length; j++)
                    builder.Add((Target(instr, ops[oi++]), EdgeKind.SwitchCase, j));
                if (oi < ops.Length)
                    builder.Add((Target(instr, ops[oi]), EdgeKind.SwitchDefault, null));
                return new InstructionShape(false, builder.ToImmutable());
            }

            case HlOpcode.Trap:
                // Handler entry is a distinguished Exception edge, not ordinary flow.
                return new InstructionShape(true,
                    ops.Length >= 2 ? [(Target(instr, ops[1]), EdgeKind.Exception, null)] : []);

            case HlOpcode.Catch:
                // No extra edge: already modeled by the matching Trap's Exception edge.
                return new InstructionShape(true, []);

            default:
            {
                var kinds = HlOpcodeInfo.Operands(instr.Opcode);
                for (int i = 0; i < kinds.Length && i < ops.Length; i++)
                {
                    if (kinds[i] == HlOperandKind.JumpOffset)
                        return new InstructionShape(true,
                            [(Target(instr, ops[i]), EdgeKind.Jump, null)]);
                }
                return new InstructionShape(true, []);
            }
        }
    }

    private static int Target(HlInstruction instr, int delta) => instr.Offset + 1 + delta;
}
