using System.Text;

namespace HLX.Core.IO;

/// <summary>
/// Parses a HashLink bytecode stream (.hl / hlboot.dat) into an <see cref="HlModule"/>.
/// </summary>
public static class HlReader
{
    public static HlModule Read(Stream stream)
    {
        var r = new HlBinaryReader(stream);
        return r.ReadModule();
    }
}

internal sealed class HlBinaryReader(Stream stream)
{
    // Fixed operand count per opcode, in HlOpcode order; -1 = variable-arity.
    private static readonly int[] _opNargs =
    [
        2, 2, 2, 2, 2, 2, 1, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 2, 1, 1, 2, 3, 4, 5, 6, -1,
        -1, -1, -1, 2, 3, 3, 2, 2, 3, 3,
        2, 2, 3, 3, 2, 2, 2, 2, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3, 1, 2,
        2, 2, 2, 2, 2, 2, 0, 1, 1, 1,
        -1, 1, 2, 1, 3, 3, 3, 3, 3, 3,
        3, 3, 1, 2, 2, 2, 2, 2, 2, 2,
        -1, 2, 2, 4, 3, 0, 2, 3, 0, 3,
        3, 1,
    ];

    internal byte ReadByte()
    {
        int b = stream.ReadByte();
        if (b < 0) throw new EndOfStreamException();
        return (byte)b;
    }

    internal int ReadI32()
    {
        Span<byte> buf = stackalloc byte[4];
        if (stream.ReadAtLeast(buf, 4, throwOnEndOfStream: false) < 4)
            throw new EndOfStreamException();
        return buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
    }

    internal double ReadF64()
    {
        Span<byte> buf = stackalloc byte[8];
        if (stream.ReadAtLeast(buf, 8, throwOnEndOfStream: false) < 8)
            throw new EndOfStreamException();
        return BitConverter.ToDouble(buf);
    }

    // Variable-length signed integer (sign-magnitude), 1/2/4 bytes.
    internal int ReadIndex()
    {
        byte b = ReadByte();
        if ((b & 0x80) == 0)
            return b & 0x7F;
        if ((b & 0x40) == 0)
        {
            int v = ReadByte() | ((b & 31) << 8);
            return (b & 0x20) == 0 ? v : -v;
        }
        else
        {
            int c = ReadByte(), d = ReadByte(), e = ReadByte();
            int v = ((b & 31) << 24) | (c << 16) | (d << 8) | e;
            return (b & 0x20) == 0 ? v : -v;
        }
    }

    internal int ReadUIndex()
    {
        int i = ReadIndex();
        if (i < 0) throw new InvalidDataException($"Negative index {i} where unsigned expected.");
        return i;
    }

    private string[] ReadStringPool(int count)
    {
        int totalBytes = ReadI32();
        var data = new byte[totalBytes];
        stream.ReadExactly(data);

        var result = new string[count];
        int offset = 0;
        for (int i = 0; i < count; i++)
        {
            int len = ReadUIndex();
            result[i] = Encoding.UTF8.GetString(data, offset, len);
            offset += len + 1;
        }
        return result;
    }

    internal HlModule ReadModule()
    {
        if (ReadByte() != 0x48 || ReadByte() != 0x4C || ReadByte() != 0x42)
            throw new InvalidDataException("Not a HashLink bytecode file (bad magic).");

        int version = ReadByte();
        if (version < 2 || version > 5)
            throw new InvalidDataException($"Unsupported bytecode version {version} (supported: 2–5).");

        int flags      = ReadUIndex();
        int nints      = ReadUIndex();
        int nfloats    = ReadUIndex();
        int nstrings   = ReadUIndex();
        int nbytes     = version >= 5 ? ReadUIndex() : 0;
        int ntypes     = ReadUIndex();
        int nglobals   = ReadUIndex();
        int nnatives   = ReadUIndex();
        int nfunctions = ReadUIndex();
        int nconstants = version >= 4 ? ReadUIndex() : 0;
        int entrypoint = ReadUIndex();

        bool hasDebug = (flags & 1) != 0;
        var header = new HlHeader(version, hasDebug ? HlFeatureFlags.HasDebugInfo : HlFeatureFlags.None);

        var ints = new int[nints];
        for (int i = 0; i < nints; i++) ints[i] = ReadI32();

        var floats = new double[nfloats];
        for (int i = 0; i < nfloats; i++) floats[i] = ReadF64();

        var strings = ReadStringPool(nstrings);

        // Bytes pool (v5+)
        ImmutableArray<byte[]> bytesPool;
        if (version >= 5)
        {
            int total = ReadI32();
            var raw   = new byte[total];
            stream.ReadExactly(raw);
            var positions = new int[nbytes];
            for (int i = 0; i < nbytes; i++) positions[i] = ReadUIndex();

            var bufs = new byte[nbytes][];
            for (int i = 0; i < nbytes; i++)
            {
                int start = positions[i];
                int end   = i + 1 < nbytes ? positions[i + 1] : total;
                bufs[i]   = raw[start..end];
            }
            bytesPool = [..bufs];
        }
        else
        {
            bytesPool = [];
        }

        string[] debugFiles = [];
        if (hasDebug)
        {
            int nfiles = ReadUIndex();
            debugFiles = ReadStringPool(nfiles);
        }

        var types = new HlType[ntypes];
        for (int i = 0; i < ntypes; i++)
            types[i] = ReadType(strings);

        var globals = new int[nglobals];
        for (int i = 0; i < nglobals; i++) globals[i] = ReadUIndex();

        var natives = new HlNative[nnatives];
        for (int i = 0; i < nnatives; i++)
            natives[i] = ReadNative(types, strings);

        var functions = new HlFunction[nfunctions];
        for (int i = 0; i < nfunctions; i++)
            functions[i] = ReadFunction(types, hasDebug, version);

        // Constants table (v4+): bookkeeping only, not modeled.
        for (int i = 0; i < nconstants; i++)
        {
            ReadUIndex();
            int nfields = ReadUIndex();
            for (int j = 0; j < nfields; j++) ReadUIndex();
        }

        return new HlModule(
            header,
            [..ints], [..floats], [..strings],
            bytesPool,
            [..types], [..natives], [..functions],
            [..globals], [..debugFiles],
            entrypoint);
    }

    private HlType ReadType(string[] strings)
    {
        int kind = ReadUIndex();
        return kind switch
        {
            0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 12 or 13 or 16 or 23
                => new PrimitiveType((PrimitiveKind)kind),

            10 or 20
                => ReadFunctionType(isMethod: kind == 20),

            11 or 21
                => ReadObjectType(strings, isStruct: kind == 21),

            15 => ReadVirtualType(strings),

            17 => new AbstractType(strings[ReadUIndex()]),

            18 => ReadEnumType(strings),

            14 => new ReferenceType(ReferenceKind.Ref,    ReadUIndex()),
            19 => new ReferenceType(ReferenceKind.Null,   ReadUIndex()),
            22 => new ReferenceType(ReferenceKind.Packed, ReadUIndex()),

            _ => throw new InvalidDataException($"Unknown type kind {kind}.")
        };
    }

    private FunctionType ReadFunctionType(bool isMethod)
    {
        int nargs = ReadByte();
        var args  = new int[nargs];
        for (int i = 0; i < nargs; i++) args[i] = ReadUIndex();
        int ret = ReadUIndex();
        return new FunctionType([..args], ret, isMethod);
    }

    private ObjectType ReadObjectType(string[] strings, bool isStruct)
    {
        string name     = strings[ReadUIndex()];
        int    superRaw = ReadIndex();   // -1 = no super
        int    global   = ReadUIndex();  // 0 = none

        int nfields   = ReadUIndex();
        int nproto    = ReadUIndex();
        int nbindings = ReadUIndex();

        var fields = new HlField[nfields];
        for (int i = 0; i < nfields; i++)
            fields[i] = new HlField(strings[ReadUIndex()], ReadUIndex());

        var protos = new HlProto[nproto];
        for (int i = 0; i < nproto; i++)
            protos[i] = new HlProto(strings[ReadUIndex()], ReadUIndex(), ReadIndex());

        var bindings = new HlBinding[nbindings];
        for (int i = 0; i < nbindings; i++)
            bindings[i] = new HlBinding(ReadUIndex(), ReadUIndex());

        return new ObjectType(name, superRaw < 0 ? null : superRaw, global,
            [..fields], [..protos], [..bindings], isStruct);
    }

    private VirtualType ReadVirtualType(string[] strings)
    {
        int nfields = ReadUIndex();
        var fields  = new HlField[nfields];
        for (int i = 0; i < nfields; i++)
            fields[i] = new HlField(strings[ReadUIndex()], ReadUIndex());
        return new VirtualType([..fields]);
    }

    private EnumType ReadEnumType(string[] strings)
    {
        string name    = strings[ReadUIndex()];
        int    global  = ReadUIndex();
        int    nconstr = ReadUIndex();

        var constructs = new HlEnumConstruct[nconstr];
        for (int i = 0; i < nconstr; i++)
        {
            string cname   = strings[ReadUIndex()];
            int    nparams = ReadUIndex();
            var    ps      = new int[nparams];
            for (int j = 0; j < nparams; j++) ps[j] = ReadUIndex();
            constructs[i] = new HlEnumConstruct(cname, [..ps]);
        }
        return new EnumType(name, global, [..constructs]);
    }

    private static HlType TypeAt(HlType[] types, int index)
    {
        if ((uint)index >= (uint)types.Length)
            throw new InvalidDataException($"Type index {index} out of range (ntypes={types.Length}).");
        return types[index]!;
    }

    private HlNative ReadNative(HlType[] types, string[] strings)
    {
        string lib    = strings[ReadUIndex()];
        string name   = strings[ReadUIndex()];
        var    type   = (FunctionType)TypeAt(types, ReadUIndex());
        int    findex = ReadUIndex();
        return new HlNative(lib, name, type, findex);
    }

    private HlFunction ReadFunction(HlType[] types, bool hasDebug, int version)
    {
        var type   = (FunctionType)TypeAt(types, ReadUIndex());
        int findex = ReadUIndex();
        int nregs  = ReadUIndex();
        int nops   = ReadUIndex();

        var regs = new HlType[nregs];
        for (int i = 0; i < nregs; i++) regs[i] = TypeAt(types, ReadUIndex());

        var ops = new HlInstruction[nops];
        for (int i = 0; i < nops; i++) ops[i] = ReadInstruction(i);

        ImmutableArray<HlDebugInfo> debugInfo = [];
        if (hasDebug)
        {
            debugInfo = [..ReadDebugInfo(nops)];

            if (version >= 3)
            {
                int nassigns = ReadUIndex();
                for (int i = 0; i < nassigns; i++) { ReadUIndex(); ReadIndex(); }
            }
        }

        return new HlFunction(type, findex, [..regs], [..ops], debugInfo);
    }

    private HlInstruction ReadInstruction(int offset)
    {
        byte opByte = ReadByte();
        if (opByte >= (byte)HlOpcode.Last)
            throw new InvalidDataException($"Unknown opcode 0x{opByte:X2} at instruction {offset}.");

        var op    = (HlOpcode)opByte;
        int nargs = _opNargs[opByte];

        ImmutableArray<int> operands;
        if (nargs >= 0)
        {
            if (nargs == 0)
            {
                operands = ImmutableArray<int>.Empty;
            }
            else
            {
                var buf = new int[nargs];
                for (int i = 0; i < nargs; i++) buf[i] = ReadIndex();
                operands = [..buf];
            }
        }
        else
        {
            operands = op == HlOpcode.Switch ? ReadSwitchOperands() : ReadVariableCallOperands();
        }

        return new HlInstruction(op, operands, offset);
    }

    // Stores as [dst, fn, n, arg0, ..., argN-1]; n is a raw byte, not a VarInt.
    private ImmutableArray<int> ReadVariableCallOperands()
    {
        int dst = ReadIndex();
        int fn  = ReadIndex();
        int n   = ReadByte();
        var buf = new int[3 + n];
        buf[0] = dst; buf[1] = fn; buf[2] = n;
        for (int i = 0; i < n; i++) buf[3 + i] = ReadIndex();
        return [..buf];
    }

    // Stores as [src, n, offset0, ..., offsetN-1, default_j].
    private ImmutableArray<int> ReadSwitchOperands()
    {
        int src = ReadUIndex();
        int n   = ReadUIndex();
        var buf = new int[3 + n];
        buf[0] = src; buf[1] = n;
        for (int i = 0; i < n; i++) buf[2 + i] = ReadUIndex();
        buf[2 + n] = ReadUIndex();
        return [..buf];
    }

    private HlDebugInfo[] ReadDebugInfo(int nops)
    {
        var result  = new HlDebugInfo[nops];
        int curFile = -1, curLine = 0;
        int i       = 0;

        while (i < nops)
        {
            byte c = ReadByte();

            if ((c & 1) != 0)
            {
                // File-change marker; does not advance i.
                c >>= 1;
                curFile = (c << 8) | ReadByte();
            }
            else if ((c & 2) != 0)
            {
                int delta = c >> 6;
                int count = (c >> 2) & 15;
                while (count-- > 0) result[i++] = new HlDebugInfo(curFile, curLine);
                curLine += delta;
            }
            else if ((c & 4) != 0)
            {
                curLine += c >> 3;
                result[i++] = new HlDebugInfo(curFile, curLine);
            }
            else
            {
                byte b2 = ReadByte(), b3 = ReadByte();
                curLine = (c >> 3) | (b2 << 5) | (b3 << 13);
                result[i++] = new HlDebugInfo(curFile, curLine);
            }
        }

        return result;
    }
}
