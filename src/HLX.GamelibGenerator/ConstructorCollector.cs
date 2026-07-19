using System.Collections.Immutable;
using HLX.Core;

namespace HLX.GamelibGenerator;

/// <summary>
/// Recovers each class's real constructor findex via bytecode scan: HL's <c>New</c>
/// opcode is bare allocation with no constructor reference, so the only signal is a
/// <c>New dst</c> followed by a Call-family instruction whose first argument is that
/// same <c>dst</c>. Only classes with exactly one distinct candidate findex
/// module-wide are resolved; zero or ambiguous candidates are skipped.
/// </summary>
internal sealed class ConstructorCollector
{
    public Dictionary<int, int> ConstructorFindexByTypeIndex { get; } = [];

    public int TotalCandidateSitesFound;
    public int ClassesResolved;
    public int ClassesAmbiguous;

    public ConstructorCollector(HlModule module)
    {
        // Reference equality is correct here (and cheaper than ObjectType's structural
        // equality): a register's declared type IS module.Types[idx], the same instance.
        var typeIndexByInstance = new Dictionary<HlType, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < module.Types.Length; i++)
            if (module.Types[i] is ObjectType) typeIndexByInstance[module.Types[i]] = i;

        var candidatesByTypeIndex = new Dictionary<int, HashSet<int>>();

        foreach (var fn in module.Functions)
        {
            var instrs = fn.Instructions;
            for (int i = 0; i < instrs.Length; i++)
            {
                var ins = instrs[i];
                if (ins.Opcode != HlOpcode.New) continue;
                int dstReg = ins.Operands[0];
                if ((uint)dstReg >= (uint)fn.Registers.Length) continue;
                if (!typeIndexByInstance.TryGetValue(fn.Registers[dstReg], out var typeIndex)) continue;

                int? findex = FindPairedConstructorCall(instrs, i + 1, dstReg);
                if (findex == null) continue;

                TotalCandidateSitesFound++;
                if (!candidatesByTypeIndex.TryGetValue(typeIndex, out var set))
                    candidatesByTypeIndex[typeIndex] = set = [];
                set.Add(findex.Value);
            }
        }

        foreach (var (typeIndex, findexes) in candidatesByTypeIndex)
        {
            if (findexes.Count == 1)
            {
                ConstructorFindexByTypeIndex[typeIndex] = findexes.First();
                ClassesResolved++;
            }
            else
            {
                ClassesAmbiguous++;
            }
        }
    }

    // Nearest Call-family instruction after New taking dstReg as its first argument;
    // checked before the clobber check so a same-numbered void return register isn't
    // mistaken for a clobber.
    private static int? FindPairedConstructorCall(ImmutableArray<HlInstruction> instrs, int start, int dstReg)
    {
        for (int j = start; j < instrs.Length; j++)
        {
            var ins = instrs[j];
            var (findex, firstArgReg) = ExtractCallInfo(ins);
            if (findex != null && firstArgReg == dstReg) return findex;
            if (WritesRegister(ins, dstReg)) return null;
        }
        return null;
    }

    private static (int? Findex, int FirstArgReg) ExtractCallInfo(HlInstruction ins) => ins.Opcode switch
    {
        HlOpcode.Call0 => (ins.Operands[1], -1),
        HlOpcode.Call1 => (ins.Operands[1], ins.Operands[2]),
        HlOpcode.Call2 => (ins.Operands[1], ins.Operands[2]),
        HlOpcode.Call3 => (ins.Operands[1], ins.Operands[2]),
        HlOpcode.Call4 => (ins.Operands[1], ins.Operands[2]),
        // operands: [dst, findex, nArgs, args...]
        HlOpcode.CallN => (ins.Operands[1], ins.Operands.Length > 3 ? ins.Operands[3] : -1),
        _ => (null, -1)
    };

    // Opcodes whose first operand is NOT a destination register (a condition, jump
    // target, ...) or that write no register at all; every other opcode's operands[0] is.
    private static bool WritesRegister(HlInstruction ins, int reg)
    {
        switch (ins.Opcode)
        {
            case HlOpcode.SetField:
            case HlOpcode.SetThis:
            case HlOpcode.SetGlobal:
            case HlOpcode.DynSet:
            case HlOpcode.JTrue:
            case HlOpcode.JFalse:
            case HlOpcode.JNull:
            case HlOpcode.JNotNull:
            case HlOpcode.JSLt:
            case HlOpcode.JSGte:
            case HlOpcode.JSGt:
            case HlOpcode.JSLte:
            case HlOpcode.JULt:
            case HlOpcode.JUGte:
            case HlOpcode.JNotLt:
            case HlOpcode.JNotGte:
            case HlOpcode.JEq:
            case HlOpcode.JNotEq:
            case HlOpcode.JAlways:
            case HlOpcode.Label:
            case HlOpcode.Ret:
            case HlOpcode.Throw:
            case HlOpcode.Rethrow:
            case HlOpcode.Switch:
            case HlOpcode.NullCheck:
            case HlOpcode.Trap:
            case HlOpcode.EndTrap:
            case HlOpcode.SetI8:
            case HlOpcode.SetI16:
            case HlOpcode.SetMem:
            case HlOpcode.SetArray:
            case HlOpcode.SetEnumField:
            case HlOpcode.Assert:
            case HlOpcode.Nop:
            case HlOpcode.Prefetch:
            case HlOpcode.Asm:
            case HlOpcode.Catch:
                return false;
            default:
                return ins.Operands.Length > 0 && ins.Operands[0] == reg;
        }
    }
}
