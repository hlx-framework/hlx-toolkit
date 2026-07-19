using HLX.Core;
using HLX.Core.IO;
using HLX.Viewer;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: hlx-viewer <file> [types [filter] | funcs [filter] | disasm <findex> | class <name>]");
    return 1;
}

string path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 1;
}

HlModule module;
try
{
    using var fs = File.OpenRead(path);
    module = HlReader.Read(fs);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error reading {path}: {ex.Message}");
    return 1;
}

var funcNames = BuildFunctionNames(module);
var disasm = new Disassembler(module, funcNames);
string cmd = args.Length > 1 ? args[1].ToLowerInvariant() : "";
string filter = args.Length > 2 ? args[2] : "";

return cmd switch
{
    "" or "summary" => Summary(module, funcNames, disasm),
    "types"         => ListTypes(module, disasm, filter),
    "funcs"         => ListFuncs(module, funcNames, disasm, filter),
    "disasm"        => Disasm(module, disasm, args),
    "class"         => ClassDetail(module, disasm, filter),
    _               => Fail($"Unknown command: {cmd}")
};

static Dictionary<int, string> BuildFunctionNames(HlModule module)
{
    var names = new Dictionary<int, string>();
    foreach (var type in module.Types)
    {
        if (type is ObjectType obj)
        {
            foreach (var proto in obj.Protos)
                names.TryAdd(proto.FunctionIndex, $"{obj.Name}::{proto.Name}");
        }
    }
    return names;
}

static int Fail(string msg) { Console.Error.WriteLine(msg); return 1; }

static int Summary(HlModule m, Dictionary<int, string> names, Disassembler d)
{
    Console.WriteLine($"HashLink bytecode  v{m.Header.Version}  {m.Header.Flags}");
    Console.WriteLine($"  ints:      {m.Ints.Length}");
    Console.WriteLine($"  floats:    {m.Floats.Length}");
    Console.WriteLine($"  strings:   {m.Strings.Length}");
    Console.WriteLine($"  bytes:     {m.Bytes.Length}");
    Console.WriteLine($"  types:     {m.Types.Length}");
    Console.WriteLine($"  globals:   {m.Globals.Length}");
    Console.WriteLine($"  natives:   {m.Natives.Length}");
    Console.WriteLine($"  functions: {m.Functions.Length}");
    Console.WriteLine($"  dbg files: {m.DebugFiles.Length}");

    var ep = m.Functions.FirstOrDefault(f => f.FunctionIndex == m.EntryPoint);
    if (ep != null)
    {
        string epName = names.TryGetValue(m.EntryPoint, out var n) ? n : $"fn#{m.EntryPoint}";
        Console.WriteLine();
        Console.WriteLine($"  entry: fn#{m.EntryPoint}  {epName}  {d.FormatFuncType(ep.Type)}");
    }
    return 0;
}

static int ListTypes(HlModule m, Disassembler d, string filter)
{
    int shown = 0;
    for (int i = 0; i < m.Types.Length; i++)
    {
        string name = d.FormatType(m.Types[i]);
        if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        Console.WriteLine($"  [{i,4}] {name}");
        shown++;
    }
    if (filter.Length > 0)
        Console.WriteLine($"  ({shown}/{m.Types.Length} shown)");
    return 0;
}

static int ListFuncs(HlModule m, Dictionary<int, string> names, Disassembler d, string filter)
{
    Console.WriteLine("  natives:");
    foreach (var n in m.Natives.OrderBy(x => x.FunctionIndex))
    {
        string label = $"{n.Lib}.{n.Name}";
        if (filter.Length > 0 && !label.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        Console.WriteLine($"    fn#{n.FunctionIndex,-5} {label}  {d.FormatFuncType(n.Type)}");
    }
    Console.WriteLine();
    Console.WriteLine("  functions:");
    foreach (var fn in m.Functions.OrderBy(f => f.FunctionIndex))
    {
        string label = names.TryGetValue(fn.FunctionIndex, out var name) ? name : $"fn#{fn.FunctionIndex}";
        if (filter.Length > 0 && !label.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        string ep = fn.FunctionIndex == m.EntryPoint ? "  [entry]" : "";
        Console.WriteLine($"    fn#{fn.FunctionIndex,-5} {label}  {d.FormatFuncType(fn.Type)}{ep}");
    }
    return 0;
}

// Exhaustive detail needed to verify an extern stub against hashlink's
// check_same_obj type-equality check (module.c); pindex is the sharp edge.
static int ClassDetail(HlModule m, Disassembler d, string name)
{
    if (name.Length == 0) return Fail("Usage: hlx-viewer <file> class <name>");
    int idx = -1;
    for (int i = 0; i < m.Types.Length; i++)
        if (m.Types[i] is ObjectType o && o.Name == name) { idx = i; break; }
    if (idx < 0) {
        for (int i = 0; i < m.Types.Length; i++)
            if (m.Types[i] is EnumType e && e.Name == name) {
                Console.WriteLine($"Type #{i}: {name}  (enum, not obj/struct - check_same_type only compares its name)");
                Console.WriteLine($"  constructs ({e.Constructs.Length}):");
                for (int j = 0; j < e.Constructs.Length; j++)
                    Console.WriteLine($"    [{j,3}] {e.Constructs[j].Name}");
                return 0;
            }
        return Fail($"Class not found (exact match required, obj/struct or enum): {name}");
    }

    Console.WriteLine($"Type #{idx}: {name}");
    Console.WriteLine();
    Console.WriteLine("  super chain (root last):");
    int? cur = idx;
    while (cur.HasValue && m.Types[cur.Value] is ObjectType co)
    {
        Console.WriteLine($"    [{cur.Value,4}] {co.Name}  fields={co.Fields.Length} protos={co.Protos.Length} bindings={co.Bindings.Length}");
        cur = co.SuperIndex;
    }

    var target = (ObjectType)m.Types[idx];

    Console.WriteLine();
    Console.WriteLine($"  fields ({target.Fields.Length}):");
    for (int i = 0; i < target.Fields.Length; i++)
    {
        var f = target.Fields[i];
        string typeName = (uint)f.TypeIndex < (uint)m.Types.Length ? d.FormatType(m.Types[f.TypeIndex]) : "?";
        Console.WriteLine($"    [{i,3}] {f.Name}: {typeName}");
    }

    Console.WriteLine();
    Console.WriteLine($"  protos ({target.Protos.Length}):");
    for (int i = 0; i < target.Protos.Length; i++)
    {
        var p = target.Protos[i];
        Console.WriteLine($"    [{i,3}] {p.Name,-24} findex={p.FunctionIndex,-6} pindex={p.PrototypeIndex}");
    }

    if (target.Bindings.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"  bindings ({target.Bindings.Length}):");
        foreach (var b in target.Bindings)
            Console.WriteLine($"    field[{b.FieldIndex}] -> findex={b.FunctionIndex}");
    }

    return 0;
}

static int Disasm(HlModule m, Disassembler d, string[] args)
{
    if (args.Length < 3) return Fail("Usage: hlx-viewer <file> disasm <findex>");
    if (!int.TryParse(args[2], out int findex)) return Fail($"Invalid findex: {args[2]}");

    var fn = m.Functions.FirstOrDefault(f => f.FunctionIndex == findex);
    if (fn == null) return Fail($"No function with findex {findex}. Try 'funcs' to list available indices.");

    d.Print(fn, Console.Out);
    return 0;
}
