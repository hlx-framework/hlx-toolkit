using System.Collections.Immutable;
using System.Text;
using HLX.Core;

namespace HLX.Viewer;

internal sealed class Disassembler
{
    private readonly HlModule _module;
    private readonly Dictionary<int, string> _funcNames;
    private readonly Dictionary<int, HlNative> _nativeIndex;

    public Disassembler(HlModule module, Dictionary<int, string> funcNames)
    {
        _module = module;
        _funcNames = funcNames;
        _nativeIndex = module.Natives.ToDictionary(n => n.FunctionIndex);
    }

    public void Print(HlFunction fn, TextWriter w)
    {
        string label = _funcNames.TryGetValue(fn.FunctionIndex, out var name) ? name : $"fn#{fn.FunctionIndex}";
        w.WriteLine($"fn#{fn.FunctionIndex}  {label}  {FormatFuncType(fn.Type)}");
        w.WriteLine($"  regs: {fn.Registers.Length}   ops: {fn.Instructions.Length}");

        if (fn.DebugInfo.Length > 0)
        {
            var d = fn.DebugInfo[0];
            if ((uint)d.FileIndex < (uint)_module.DebugFiles.Length)
                w.WriteLine($"  src:  {_module.DebugFiles[d.FileIndex]}:{d.Line}");
        }

        w.WriteLine();
        w.WriteLine("  registers:");
        for (int i = 0; i < fn.Registers.Length; i++)
            w.WriteLine($"    r{i,-3} {FormatType(fn.Registers[i])}");

        w.WriteLine();
        w.WriteLine("  instructions:");
        int pad = fn.Instructions.Length > 0 ? fn.Instructions[^1].Offset.ToString().Length : 1;
        foreach (var instr in fn.Instructions)
        {
            string opName = HlOpcodeInfo.Name(instr.Opcode);
            string operands = FormatOperands(instr, fn);
            string body = operands.Length > 0 ? $"{opName,-16} {operands}" : opName;

            string debugSuffix = "";
            if ((uint)instr.Offset < (uint)fn.DebugInfo.Length)
            {
                var d = fn.DebugInfo[instr.Offset];
                if ((uint)d.FileIndex < (uint)_module.DebugFiles.Length)
                    debugSuffix = $"  ; {_module.DebugFiles[d.FileIndex]}:{d.Line}";
            }

            w.WriteLine($"    {instr.Offset.ToString().PadLeft(pad)}: {body}{debugSuffix}");
        }
    }

    private string FormatOperands(HlInstruction instr, HlFunction fn)
    {
        ImmutableArray<HlOperandKind> kinds = HlOpcodeInfo.Operands(instr.Opcode);
        if (kinds.IsEmpty) return "";

        var sb = new StringBuilder();
        var ops = instr.Operands;
        int oi = 0;

        for (int ki = 0; ki < kinds.Length; ki++)
        {
            if (ki > 0) sb.Append(", ");
            switch (kinds[ki])
            {
                case HlOperandKind.Register:
                    AppendReg(sb, ops[oi++], fn);
                    break;

                case HlOperandKind.IntConst:
                {
                    int idx = ops[oi++];
                    sb.Append((uint)idx < (uint)_module.Ints.Length ? _module.Ints[idx].ToString() : $"int[{idx}]");
                    break;
                }
                case HlOperandKind.FloatConst:
                {
                    int idx = ops[oi++];
                    sb.Append((uint)idx < (uint)_module.Floats.Length ? _module.Floats[idx].ToString("G") : $"float[{idx}]");
                    break;
                }
                case HlOperandKind.StringConst:
                {
                    int idx = ops[oi++];
                    string s = (uint)idx < (uint)_module.Strings.Length ? _module.Strings[idx] : "?";
                    sb.Append('"').Append(Escape(s)).Append('"');
                    break;
                }
                case HlOperandKind.BytesConst:
                    sb.Append($"bytes[{ops[oi++]}]");
                    break;

                case HlOperandKind.TypeIndex:
                {
                    int idx = ops[oi++];
                    sb.Append((uint)idx < (uint)_module.Types.Length ? FormatType(_module.Types[idx]) : $"type[{idx}]");
                    break;
                }
                case HlOperandKind.GlobalIndex:
                {
                    int gi = ops[oi++];
                    sb.Append($"global[{gi}]");
                    if ((uint)gi < (uint)_module.Globals.Length)
                    {
                        int ti = _module.Globals[gi];
                        if ((uint)ti < (uint)_module.Types.Length)
                            sb.Append(':').Append(FormatType(_module.Types[ti]));
                    }
                    break;
                }
                case HlOperandKind.FunctionRef:
                    sb.Append(ResolveFuncRef(ops[oi++]));
                    break;

                case HlOperandKind.FieldIndex:
                    sb.Append($"field[{ops[oi++]}]");
                    break;

                case HlOperandKind.JumpOffset:
                {
                    int delta = ops[oi++];
                    sb.Append($"@{instr.Offset + 1 + delta}");
                    break;
                }
                case HlOperandKind.SwitchTable:
                {
                    int n = ops[oi++];
                    sb.Append('{');
                    for (int j = 0; j < n; j++)
                    {
                        if (j > 0) sb.Append(", ");
                        sb.Append($"@{instr.Offset + 1 + ops[oi++]}");
                    }
                    sb.Append($"; default:@{instr.Offset + 1 + ops[oi++]}}}");
                    break;
                }
                case HlOperandKind.CallArgs:
                {
                    int n = ops[oi++];
                    sb.Append('(');
                    for (int j = 0; j < n; j++)
                    {
                        if (j > 0) sb.Append(", ");
                        AppendReg(sb, ops[oi++], fn);
                    }
                    sb.Append(')');
                    break;
                }
                case HlOperandKind.Inline:
                    sb.Append(ops[oi++]);
                    break;
            }
        }
        return sb.ToString();
    }

    private void AppendReg(StringBuilder sb, int idx, HlFunction fn)
    {
        sb.Append('r').Append(idx);
        if ((uint)idx < (uint)fn.Registers.Length)
            sb.Append(':').Append(FormatType(fn.Registers[idx]));
    }

    private string ResolveFuncRef(int findex)
    {
        if (_nativeIndex.TryGetValue(findex, out var n)) return $"{n.Lib}.{n.Name}";
        if (_funcNames.TryGetValue(findex, out var name)) return name;
        return $"fn#{findex}";
    }

    internal string FormatType(HlType type) => type switch
    {
        PrimitiveType p => p.Kind switch
        {
            PrimitiveKind.Void   => "void",
            PrimitiveKind.U8     => "u8",
            PrimitiveKind.U16    => "u16",
            PrimitiveKind.I32    => "i32",
            PrimitiveKind.I64    => "i64",
            PrimitiveKind.F32    => "f32",
            PrimitiveKind.F64    => "f64",
            PrimitiveKind.Bool   => "bool",
            PrimitiveKind.Bytes  => "bytes",
            PrimitiveKind.Dyn    => "dyn",
            PrimitiveKind.Array  => "array",
            PrimitiveKind.Type   => "type",
            PrimitiveKind.DynObj => "dynobj",
            PrimitiveKind.Guid   => "guid",
            _ => p.Kind.ToString()
        },
        FunctionType f   => FormatFuncType(f),
        ObjectType o     => o.Name,
        VirtualType      => "virtual",
        EnumType e       => e.Name,
        AbstractType a   => a.Name,
        ReferenceType r  =>
            $"{r.Kind.ToString().ToLower()}<{((uint)r.InnerTypeIndex < (uint)_module.Types.Length ? FormatType(_module.Types[r.InnerTypeIndex]) : "?")}>",
        _ => "?"
    };

    internal string FormatFuncType(FunctionType f)
    {
        var args = string.Join(", ", f.ArgTypes.Select(i =>
            (uint)i < (uint)_module.Types.Length ? FormatType(_module.Types[i]) : "?"));
        var ret = (uint)f.ReturnType < (uint)_module.Types.Length
            ? FormatType(_module.Types[f.ReturnType]) : "?";
        string prefix = f.IsMethod ? "method" : "fun";
        return $"{prefix}({args}) -> {ret}";
    }

    private static string Escape(string s)
    {
        if (s.Length > 48) s = s[..48] + "…";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}
