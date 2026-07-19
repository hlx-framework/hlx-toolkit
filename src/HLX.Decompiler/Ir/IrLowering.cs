namespace HLX.Decompiler;

// Control-transfer opcodes (jumps, Switch, Trap/EndTrap/Catch) return null:
// AstBuilder reconstructs them as If/While/Switch/Try from the CFG instead.
public sealed class IrLowering(HlFunction fn, HlModule module, IReadOnlyDictionary<int, string> funcNames)
{
    private static readonly HlType UnknownType = new AbstractType("?");

    public IrStatement? Lower(HlInstruction instr)
    {
        var ops = instr.Operands;

        switch (instr.Opcode)
        {
            case HlOpcode.Mov:    return Assign(ops[0], Reg(ops[1]));
            case HlOpcode.Int:    return Assign(ops[0], IntVal(ops[1]));
            case HlOpcode.Float:  return Assign(ops[0], FloatVal(ops[1]));
            case HlOpcode.Bool:   return Assign(ops[0], new BoolLiteral(ops[1] != 0));
            case HlOpcode.Bytes:  return Assign(ops[0], new BytesLiteral(ops[1]));
            case HlOpcode.String: return Assign(ops[0], StrVal(ops[1]));
            case HlOpcode.Null:   return Assign(ops[0], new NullLiteral());

            case HlOpcode.Add:  return Assign(ops[0], new BinaryExpr(BinaryOp.Add, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.Sub:  return Assign(ops[0], new BinaryExpr(BinaryOp.Sub, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.Mul:  return Assign(ops[0], new BinaryExpr(BinaryOp.Mul, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.SDiv: return Assign(ops[0], new BinaryExpr(BinaryOp.SDiv, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.UDiv: return Assign(ops[0], new BinaryExpr(BinaryOp.UDiv, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.SMod: return Assign(ops[0], new BinaryExpr(BinaryOp.SMod, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.UMod: return Assign(ops[0], new BinaryExpr(BinaryOp.UMod, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.Shl:  return Assign(ops[0], new BinaryExpr(BinaryOp.Shl, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.SShr: return Assign(ops[0], new BinaryExpr(BinaryOp.SShr, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.UShr: return Assign(ops[0], new BinaryExpr(BinaryOp.UShr, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.And:  return Assign(ops[0], new BinaryExpr(BinaryOp.And, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.Or:   return Assign(ops[0], new BinaryExpr(BinaryOp.Or, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.Xor:  return Assign(ops[0], new BinaryExpr(BinaryOp.Xor, Reg(ops[1]), Reg(ops[2])));
            case HlOpcode.Neg:  return Assign(ops[0], new UnaryExpr(UnaryOp.Neg, Reg(ops[1])));
            case HlOpcode.Not:  return Assign(ops[0], new UnaryExpr(UnaryOp.Not, Reg(ops[1])));
            case HlOpcode.Incr: return new IncrStatement(Reg(ops[0]), true);
            case HlOpcode.Decr: return new IncrStatement(Reg(ops[0]), false);

            case HlOpcode.Call0: return Assign(ops[0], new CallExpr(StaticRef(ops[1]), []));
            case HlOpcode.Call1: return Assign(ops[0], new CallExpr(StaticRef(ops[1]), [Reg(ops[2])]));
            case HlOpcode.Call2: return Assign(ops[0], new CallExpr(StaticRef(ops[1]), [Reg(ops[2]), Reg(ops[3])]));
            case HlOpcode.Call3: return Assign(ops[0], new CallExpr(StaticRef(ops[1]), [Reg(ops[2]), Reg(ops[3]), Reg(ops[4])]));
            case HlOpcode.Call4: return Assign(ops[0], new CallExpr(StaticRef(ops[1]), [Reg(ops[2]), Reg(ops[3]), Reg(ops[4]), Reg(ops[5])]));
            case HlOpcode.CallN:
            {
                int dst = ops[0], fi = ops[1], oi = 2;
                return Assign(dst, new CallExpr(StaticRef(fi), CallArgs(ops, ref oi)));
            }
            case HlOpcode.CallMethod:
            {
                if (ops.Length < 3) return new CommentStatement("CallMethod?");
                int dst = ops[0], slot = ops[1];
                var (recv, args) = CallArgsWithReceiver(ops, 2);
                if (recv < 0) return Assign(dst, new RawExpr($"<method:{slot}>()"));
                return Assign(dst, new CallExpr(new MethodRef(Reg(recv), slot, ProtoNameOf(recv, slot)), args));
            }
            case HlOpcode.CallThis:
            {
                if (ops.Length < 3) return new CommentStatement("CallThis?");
                int dst = ops[0], slot = ops[1], oi = 2;
                var args = CallArgs(ops, ref oi);
                return Assign(dst, new CallExpr(new MethodRef(new ThisExpr(RegisterType(0)), slot, ThisProtoNameOf(slot)), args));
            }
            case HlOpcode.CallClosure:
            {
                int dst = ops[0], cl = ops[1], oi = 2;
                return Assign(dst, new CallExpr(new ClosureCallRef(Reg(cl)), CallArgs(ops, ref oi)));
            }

            // A bound method value prints identically to a field access in Haxe.
            case HlOpcode.StaticClosure:   return Assign(ops[0], new FuncRefExpr(ops[1], FuncNameOf(ops[1])));
            case HlOpcode.InstanceClosure: return Assign(ops[0], new FieldAccessExpr(Reg(ops[2]), FuncNameOf(ops[1]), ops[1]));
            case HlOpcode.VirtualClosure:  return Assign(ops[0], new FieldAccessExpr(Reg(ops[1]), ProtoNameOf(ops[1], ops[2]), ops[2]));

            case HlOpcode.GetGlobal: return Assign(ops[0], new GlobalAccessExpr(ops[1]));
            case HlOpcode.SetGlobal: return new GlobalAssignStatement(ops[0], Reg(ops[1]));
            case HlOpcode.Field:     return Assign(ops[0], new FieldAccessExpr(Reg(ops[1]), FieldNameOf(ops[1], ops[2]), ops[2]));
            case HlOpcode.SetField:  return new FieldAssignStatement(Reg(ops[0]), FieldNameOf(ops[0], ops[1]), ops[1], Reg(ops[2]));
            case HlOpcode.GetThis:   return Assign(ops[0], new ThisFieldAccessExpr(FieldNameOf(0, ops[1]), ops[1]));
            case HlOpcode.SetThis:   return new ThisFieldAssignStatement(FieldNameOf(0, ops[0]), ops[0], Reg(ops[1]));
            case HlOpcode.DynGet:    return Assign(ops[0], new DynFieldAccessExpr(Reg(ops[1]), RawStrName(ops[2])));
            case HlOpcode.DynSet:    return new DynFieldAssignStatement(Reg(ops[0]), RawStrName(ops[1]), Reg(ops[2]));

            case HlOpcode.JTrue: case HlOpcode.JFalse: case HlOpcode.JNull: case HlOpcode.JNotNull:
            case HlOpcode.JSLt: case HlOpcode.JSGte: case HlOpcode.JSGt: case HlOpcode.JSLte:
            case HlOpcode.JULt: case HlOpcode.JUGte: case HlOpcode.JNotLt: case HlOpcode.JNotGte:
            case HlOpcode.JEq: case HlOpcode.JNotEq: case HlOpcode.JAlways:
                return null;

            case HlOpcode.ToDyn:      return Assign(ops[0], new CastExpr(CastKind.ToDyn, Reg(ops[1]), null));
            case HlOpcode.ToSFloat:   return Assign(ops[0], new CastExpr(CastKind.ToSFloat, Reg(ops[1]), null));
            case HlOpcode.ToUFloat:   return Assign(ops[0], new CastExpr(CastKind.ToUFloat, Reg(ops[1]), null));
            case HlOpcode.ToInt:      return Assign(ops[0], new CastExpr(CastKind.ToInt, Reg(ops[1]), null));
            case HlOpcode.SafeCast:   return Assign(ops[0], new CastExpr(CastKind.SafeCast, Reg(ops[1]), RegisterType(ops[0])));
            case HlOpcode.UnsafeCast: return Assign(ops[0], new CastExpr(CastKind.UnsafeCast, Reg(ops[1]), null));
            case HlOpcode.ToVirtual:  return Assign(ops[0], new CastExpr(CastKind.ToVirtual, Reg(ops[1]), null));

            case HlOpcode.Label: return null;
            case HlOpcode.Ret:
                return new ReturnStatement(RegisterType(ops[0]) is PrimitiveType { Kind: PrimitiveKind.Void } ? null : Reg(ops[0]));
            case HlOpcode.Throw:    return new ThrowStatement(Reg(ops[0]), false);
            case HlOpcode.Rethrow:  return new ThrowStatement(Reg(ops[0]), true);
            case HlOpcode.Switch:   return null;
            case HlOpcode.NullCheck: return new CommentStatement($"assert {RName(ops[0])} != null");
            case HlOpcode.Trap:     return null;
            case HlOpcode.EndTrap:  return null;

            case HlOpcode.GetI8:    return Assign(ops[0], new MemoryAccessExpr(Reg(ops[1]), Reg(ops[2]), MemoryKind.I8));
            case HlOpcode.GetI16:   return Assign(ops[0], new MemoryAccessExpr(Reg(ops[1]), Reg(ops[2]), MemoryKind.I16));
            case HlOpcode.GetMem:   return Assign(ops[0], new MemoryAccessExpr(Reg(ops[1]), Reg(ops[2]), MemoryKind.Mem));
            case HlOpcode.GetArray: return Assign(ops[0], new MemoryAccessExpr(Reg(ops[1]), Reg(ops[2]), MemoryKind.Array));
            case HlOpcode.SetI8:    return new MemorySetStatement(Reg(ops[0]), Reg(ops[1]), Reg(ops[2]), MemoryKind.I8);
            case HlOpcode.SetI16:   return new MemorySetStatement(Reg(ops[0]), Reg(ops[1]), Reg(ops[2]), MemoryKind.I16);
            case HlOpcode.SetMem:   return new MemorySetStatement(Reg(ops[0]), Reg(ops[1]), Reg(ops[2]), MemoryKind.Mem);
            case HlOpcode.SetArray: return new MemorySetStatement(Reg(ops[0]), Reg(ops[1]), Reg(ops[2]), MemoryKind.Array);

            case HlOpcode.New:       return Assign(ops[0], new NewObjectExpr(RegisterType(ops[0]) ?? UnknownType));
            case HlOpcode.ArraySize: return Assign(ops[0], new ArrayLengthExpr(Reg(ops[1])));
            case HlOpcode.Type:      return Assign(ops[0], TypeAt(ops[1]) is { } t ? new TypeValueExpr(t) : new RawExpr($"type[{ops[1]}]"));
            case HlOpcode.GetType:   return Assign(ops[0], new TypeOfExpr(Reg(ops[1])));
            case HlOpcode.GetTID:    return Assign(ops[0], new RawExpr($"{RName(ops[1])}.__id__"));
            case HlOpcode.Ref:       return Assign(ops[0], new RefExpr(Reg(ops[1])));
            case HlOpcode.Unref:     return Assign(ops[0], new DerefExpr(Reg(ops[1])));
            case HlOpcode.Setref:    return new SetRefStatement(Reg(ops[0]), Reg(ops[1]));

            case HlOpcode.MakeEnum:
            {
                if (ops.Length < 2) return new CommentStatement("MakeEnum?");
                int dst = ops[0], ctorIdx = ops[1], oi = 2;
                string ctor = EnumCtorNameOf(RegisterType(dst), ctorIdx);
                return Assign(dst, new MakeEnumExpr(ctor, CallArgs(ops, ref oi)));
            }
            case HlOpcode.EnumAlloc:
                return Assign(ops[0], new EnumAllocExpr(TypeAt(ops[1]) ?? UnknownType));
            case HlOpcode.EnumIndex:
                return Assign(ops[0], new EnumIndexExpr(Reg(ops[1])));
            case HlOpcode.EnumField:
            {
                if (ops.Length < 4) return new CommentStatement("EnumField?");
                var enumType = RegisterType(ops[1]);
                string ctor = EnumCtorNameOf(enumType, ops[2]);
                string field = EnumFieldNameOf(enumType, ops[2], ops[3]);
                return Assign(ops[0], new EnumFieldExpr(Reg(ops[1]), ctor, field));
            }
            case HlOpcode.SetEnumField:
                return new SetEnumFieldStatement(Reg(ops[0]), ops[1], Reg(ops[2]));

            case HlOpcode.Assert:    return new CommentStatement("assert");
            case HlOpcode.RefData:   return Assign(ops[0], new RawExpr($"{RName(ops[1])}.__data__"));
            case HlOpcode.RefOffset: return Assign(ops[0], new BinaryExpr(BinaryOp.Add, Reg(ops[1]), new IntLiteral(ops.Length > 2 ? ops[2] : 0)));
            case HlOpcode.Nop:       return null;
            case HlOpcode.Prefetch:  return new CommentStatement($"prefetch {RName(ops[0])}");
            case HlOpcode.Asm:       return new CommentStatement($"asm({(ops.Length > 0 ? ops[0] : 0)}, {(ops.Length > 1 ? ops[1] : 0)}, {(ops.Length > 2 ? ops[2] : 0)})");
            case HlOpcode.Catch:     return null;

            default: return new CommentStatement(HlOpcodeInfo.Name(instr.Opcode));
        }
    }

    // Always expressed as the jump-taken outcome, never fallthrough.
    public IrExpr ConditionFromBranch(HlInstruction instr)
    {
        var ops = instr.Operands;
        return instr.Opcode switch
        {
            HlOpcode.JTrue    => Reg(ops[0]),
            HlOpcode.JFalse   => new UnaryExpr(UnaryOp.Not, Reg(ops[0])),
            HlOpcode.JNull    => new BinaryExpr(BinaryOp.Eq, Reg(ops[0]), new NullLiteral()),
            HlOpcode.JNotNull => new BinaryExpr(BinaryOp.NotEq, Reg(ops[0]), new NullLiteral()),
            HlOpcode.JSLt     => new BinaryExpr(BinaryOp.Lt, Reg(ops[0]), Reg(ops[1])),
            HlOpcode.JSGte    => new BinaryExpr(BinaryOp.Gte, Reg(ops[0]), Reg(ops[1])),
            HlOpcode.JSGt     => new BinaryExpr(BinaryOp.Gt, Reg(ops[0]), Reg(ops[1])),
            HlOpcode.JSLte    => new BinaryExpr(BinaryOp.Lte, Reg(ops[0]), Reg(ops[1])),
            HlOpcode.JULt     => new BinaryExpr(BinaryOp.ULt, Reg(ops[0]), Reg(ops[1])),
            HlOpcode.JUGte    => new BinaryExpr(BinaryOp.UGte, Reg(ops[0]), Reg(ops[1])),
            HlOpcode.JNotLt   => new UnaryExpr(UnaryOp.Not, new BinaryExpr(BinaryOp.Lt, Reg(ops[0]), Reg(ops[1]))),
            HlOpcode.JNotGte  => new UnaryExpr(UnaryOp.Not, new BinaryExpr(BinaryOp.Gte, Reg(ops[0]), Reg(ops[1]))),
            HlOpcode.JEq      => new BinaryExpr(BinaryOp.Eq, Reg(ops[0]), Reg(ops[1])),
            HlOpcode.JNotEq   => new BinaryExpr(BinaryOp.NotEq, Reg(ops[0]), Reg(ops[1])),
            _ => throw new ArgumentException($"{instr.Opcode} is not a conditional branch", nameof(instr))
        };
    }

    public IrExpr SwitchScrutinee(HlInstruction instr) => Reg(instr.Operands[0]);

    public RegisterExpr Register(int i) => Reg(i);

    private AssignStatement Assign(int dst, IrExpr value) => new(Reg(dst), value);

    private RegisterExpr Reg(int i) => new(i, RegisterType(i) ?? UnknownType);

    private HlType? RegisterType(int i) => (uint)i < (uint)fn.Registers.Length ? fn.Registers[i] : null;

    private HlType? TypeAt(int ti) => (uint)ti < (uint)module.Types.Length ? module.Types[ti] : null;

    private IrExpr IntVal(int idx) =>
        (uint)idx < (uint)module.Ints.Length ? new IntLiteral(module.Ints[idx]) : new RawExpr($"int[{idx}]");

    private IrExpr FloatVal(int idx) =>
        (uint)idx < (uint)module.Floats.Length ? new FloatLiteral(module.Floats[idx]) : new RawExpr($"float[{idx}]");

    private IrExpr StrVal(int idx) =>
        (uint)idx < (uint)module.Strings.Length ? new StringLiteral(module.Strings[idx]) : new RawExpr($"str[{idx}]");

    private string RawStrName(int idx) =>
        (uint)idx < (uint)module.Strings.Length ? module.Strings[idx] : $"str[{idx}]";

    private string RName(int i) => $"r{i}";

    private string FieldNameOf(int regIdx, int slot)
    {
        if ((uint)regIdx >= (uint)fn.Registers.Length) return $"field[{slot}]";
        return fn.Registers[regIdx] switch
        {
            ObjectType o when (uint)slot < (uint)o.Fields.Length => o.Fields[slot].Name,
            VirtualType v when (uint)slot < (uint)v.Fields.Length => v.Fields[slot].Name,
            _ => $"field[{slot}]"
        };
    }

    private string ProtoNameOf(int regIdx, int slot)
    {
        if ((uint)regIdx >= (uint)fn.Registers.Length) return $"proto[{slot}]";
        return fn.Registers[regIdx] switch
        {
            ObjectType o when (uint)slot < (uint)o.Protos.Length => o.Protos[slot].Name,
            _ => $"proto[{slot}]"
        };
    }

    private string ThisProtoNameOf(int slot)
    {
        if (fn.Registers.IsEmpty) return $"proto[{slot}]";
        return fn.Registers[0] switch
        {
            ObjectType o when (uint)slot < (uint)o.Protos.Length => o.Protos[slot].Name,
            _ => $"proto[{slot}]"
        };
    }

    private string FuncNameOf(int fi)
    {
        if (funcNames.TryGetValue(fi, out var n)) return n;
        foreach (var nat in module.Natives)
            if (nat.FunctionIndex == fi) return $"{nat.Lib}.{nat.Name}";
        return $"fn#{fi}";
    }

    private StaticFuncRef StaticRef(int fi) => new(fi, FuncNameOf(fi));

    private ImmutableArray<IrExpr> CallArgs(ImmutableArray<int> ops, ref int oi)
    {
        if (oi >= ops.Length) return [];
        int cnt = ops[oi++];
        var builder = ImmutableArray.CreateBuilder<IrExpr>(cnt);
        for (int j = 0; j < cnt && oi < ops.Length; j++)
            builder.Add(Reg(ops[oi++]));
        return builder.ToImmutable();
    }

    private (int Receiver, ImmutableArray<IrExpr> Args) CallArgsWithReceiver(ImmutableArray<int> ops, int oi)
    {
        if (oi >= ops.Length) return (-1, []);
        int cnt = ops[oi++];
        int recv = cnt > 0 && oi < ops.Length ? ops[oi++] : -1;
        var builder = ImmutableArray.CreateBuilder<IrExpr>(Math.Max(0, cnt - 1));
        for (int j = 1; j < cnt && oi < ops.Length; j++)
            builder.Add(Reg(ops[oi++]));
        return (recv, builder.ToImmutable());
    }

    private static string EnumCtorNameOf(HlType? type, int ctorIdx) =>
        type is EnumType e && (uint)ctorIdx < (uint)e.Constructs.Length ? e.Constructs[ctorIdx].Name : $"ctor[{ctorIdx}]";

    private static string EnumFieldNameOf(HlType? type, int ctorIdx, int fieldIdx) =>
        type is EnumType e
        && (uint)ctorIdx < (uint)e.Constructs.Length
        && (uint)fieldIdx < (uint)e.Constructs[ctorIdx].ParamTypes.Length
            ? $"param{fieldIdx}"
            : $"field[{fieldIdx}]";
}
