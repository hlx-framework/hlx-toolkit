namespace HLX.Core;

public static class HlOpcodeInfo
{
    public static string Name(HlOpcode op)
        => (int)op < _entries.Length ? _entries[(int)op].Name : $"Unknown({(int)op})";

    public static ImmutableArray<HlOperandKind> Operands(HlOpcode op)
        => (int)op < _entries.Length ? _entries[(int)op].Operands : ImmutableArray<HlOperandKind>.Empty;

    private const HlOperandKind R   = HlOperandKind.Register;
    private const HlOperandKind I   = HlOperandKind.IntConst;
    private const HlOperandKind FL  = HlOperandKind.FloatConst;
    private const HlOperandKind STR = HlOperandKind.StringConst;
    private const HlOperandKind BYT = HlOperandKind.BytesConst;
    private const HlOperandKind TY  = HlOperandKind.TypeIndex;
    private const HlOperandKind GL  = HlOperandKind.GlobalIndex;
    private const HlOperandKind FN  = HlOperandKind.FunctionRef;
    private const HlOperandKind FLD = HlOperandKind.FieldIndex;
    private const HlOperandKind JMP = HlOperandKind.JumpOffset;
    private const HlOperandKind SW  = HlOperandKind.SwitchTable;
    private const HlOperandKind CA  = HlOperandKind.CallArgs;
    private const HlOperandKind INL = HlOperandKind.Inline;

    private readonly record struct OpcodeEntry(string Name, ImmutableArray<HlOperandKind> Operands);

    private static readonly OpcodeEntry[] _entries = BuildTable();

    private static OpcodeEntry[] BuildTable()
    {
        var t = new OpcodeEntry[(int)HlOpcode.Last];

        static OpcodeEntry E(string name, params HlOperandKind[] kinds)
            => new(name, [..kinds]);

        t[0]  = E("Mov",             R, R);
        t[1]  = E("Int",             R, I);
        t[2]  = E("Float",           R, FL);
        t[3]  = E("Bool",            R, INL);
        t[4]  = E("Bytes",           R, BYT);
        t[5]  = E("String",          R, STR);
        t[6]  = E("Null",            R);
        t[7]  = E("Add",             R, R, R);
        t[8]  = E("Sub",             R, R, R);
        t[9]  = E("Mul",             R, R, R);
        t[10] = E("SDiv",            R, R, R);
        t[11] = E("UDiv",            R, R, R);
        t[12] = E("SMod",            R, R, R);
        t[13] = E("UMod",            R, R, R);
        t[14] = E("Shl",             R, R, R);
        t[15] = E("SShr",            R, R, R);
        t[16] = E("UShr",            R, R, R);
        t[17] = E("And",             R, R, R);
        t[18] = E("Or",              R, R, R);
        t[19] = E("Xor",             R, R, R);
        t[20] = E("Neg",             R, R);
        t[21] = E("Not",             R, R);
        t[22] = E("Incr",            R);
        t[23] = E("Decr",            R);

        t[24] = E("Call0",           R, FN);
        t[25] = E("Call1",           R, FN, R);
        t[26] = E("Call2",           R, FN, R, R);
        t[27] = E("Call3",           R, FN, R, R, R);
        t[28] = E("Call4",           R, FN, R, R, R, R);
        t[29] = E("CallN",           R, FN, CA);
        t[30] = E("CallMethod",      R, FLD, CA);
        t[31] = E("CallThis",        R, FLD, CA);
        t[32] = E("CallClosure",     R, R, CA);
        t[33] = E("StaticClosure",   R, FN);
        t[34] = E("InstanceClosure", R, FN, R);
        t[35] = E("VirtualClosure",  R, R, FLD);

        t[36] = E("GetGlobal",       R, GL);
        t[37] = E("SetGlobal",       GL, R);
        t[38] = E("Field",           R, R, FLD);
        t[39] = E("SetField",        R, FLD, R);
        t[40] = E("GetThis",         R, FLD);
        t[41] = E("SetThis",         FLD, R);
        t[42] = E("DynGet",          R, R, STR);
        t[43] = E("DynSet",          R, STR, R);

        t[44] = E("JTrue",           R, JMP);
        t[45] = E("JFalse",          R, JMP);
        t[46] = E("JNull",           R, JMP);
        t[47] = E("JNotNull",        R, JMP);
        t[48] = E("JSLt",            R, R, JMP);
        t[49] = E("JSGte",           R, R, JMP);
        t[50] = E("JSGt",            R, R, JMP);
        t[51] = E("JSLte",           R, R, JMP);
        t[52] = E("JULt",            R, R, JMP);
        t[53] = E("JUGte",           R, R, JMP);
        t[54] = E("JNotLt",          R, R, JMP);
        t[55] = E("JNotGte",         R, R, JMP);
        t[56] = E("JEq",             R, R, JMP);
        t[57] = E("JNotEq",          R, R, JMP);
        t[58] = E("JAlways",         JMP);

        t[59] = E("ToDyn",           R, R);
        t[60] = E("ToSFloat",        R, R);
        t[61] = E("ToUFloat",        R, R);
        t[62] = E("ToInt",           R, R);
        t[63] = E("SafeCast",        R, R);
        t[64] = E("UnsafeCast",      R, R);
        t[65] = E("ToVirtual",       R, R);

        t[66] = E("Label");
        t[67] = E("Ret",             R);
        t[68] = E("Throw",           R);
        t[69] = E("Rethrow",         R);
        t[70] = E("Switch",          R, SW);
        t[71] = E("NullCheck",       R);
        t[72] = E("Trap",            R, JMP);              // R = exception register
        t[73] = E("EndTrap",         R);

        t[74] = E("GetI8",           R, R, R);
        t[75] = E("GetI16",          R, R, R);
        t[76] = E("GetMem",          R, R, R);
        t[77] = E("GetArray",        R, R, R);
        t[78] = E("SetI8",           R, R, R);
        t[79] = E("SetI16",          R, R, R);
        t[80] = E("SetMem",          R, R, R);
        t[81] = E("SetArray",        R, R, R);

        t[82] = E("New",             R);
        t[83] = E("ArraySize",       R, R);
        t[84] = E("Type",            R, TY);
        t[85] = E("GetType",         R, R);
        t[86] = E("GetTID",          R, R);
        t[87] = E("Ref",             R, R);
        t[88] = E("Unref",           R, R);
        t[89] = E("Setref",          R, R);

        t[90]  = E("MakeEnum",       R, INL, CA);
        t[91]  = E("EnumAlloc",      R, TY);
        t[92]  = E("EnumIndex",      R, R);
        t[93]  = E("EnumField",      R, R, INL, INL);      // constructIdx, fieldIdx
        t[94]  = E("SetEnumField",   R, INL, R);           // fieldIdx
        t[95]  = E("Assert");
        t[96]  = E("RefData",        R, R);
        t[97]  = E("RefOffset",      R, R, INL);
        t[98]  = E("Nop");
        t[99]  = E("Prefetch",       R, FLD, INL);
        t[100] = E("Asm",            INL, INL, INL);
        t[101] = E("Catch",          JMP);

        return t;
    }
}
