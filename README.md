# hlx-toolkit

C#/.NET tools for inspecting HashLink (`.hl`/`hlboot.dat`) bytecode and generating
typed Haxe gamelib wrappers for the `hlx-framework` modding stack. 

## HLX.Core

Library. Zero dependencies beyond .NET. Reads/writes the raw HashLink binary
format into an `HlModule` (types, functions, opcodes, strings/ints/floats
pools). Every other project builds on this model; it does no interpretation
of its own.

```csharp
using var stream = File.OpenRead("hlboot.dat");
HlModule module = HlReader.Read(stream);
```

## HLX.Analysis

Library. Depends on `HLX.Core`. Control-flow graph construction, call graphs,
and name/type/reference indexes over a parsed `HlModule` - the shared
analysis layer consumed by `HLX.Decompiler` and `HLX.App`.

## HLX.Decompiler

Library. Depends on `HLX.Core` + `HLX.Analysis`. Lowers bytecode into IR, then
into a structured AST (if/while/switch), then pretty-prints Haxe-like source
via `HaxePrinter`. Used by `HLX.App`'s decompile view and (for type-naming
only) by `HLX.GamelibGenerator`.

## HLX.Viewer

CLI. A quick, scriptable inspector - no analysis/decompilation, just raw
model dumps. Published on [NuGet.org](https://www.nuget.org/packages/HLX.Viewer)
as a .NET tool:

```
dotnet tool install -g HLX.Viewer

hlx-viewer <file.hl|hlboot.dat> types [filter]
hlx-viewer <file.hl|hlboot.dat> funcs [filter]
hlx-viewer <file.hl|hlboot.dat> disasm <findex>
hlx-viewer <file.hl|hlboot.dat> class <name>
```

Or run from source:

```
dotnet run --project src/HLX.Viewer -- <file.hl|hlboot.dat> types [filter]
```

## HLX.App

Avalonia desktop GUI. Tree-based browsing of types/functions/globals, search,
find-usages, and decompiled-source views - the graphical counterpart to
`HLX.Viewer`, built on the same `HLX.Core`/`HLX.Analysis`/`HLX.Decompiler`
layers (no parsing/analysis logic of its own).

```
dotnet run --project src/HLX.App
```

## HLX.GamelibGenerator

CLI. Depends on `HLX.Core` + `HLX.Decompiler`. Offline, dev-time tool that
reads a game's `hlboot.dat` and emits one strongly-typed Haxe
`abstract ClassName(Dynamic)` wrapper per game class (a "gamelib") for mods to
compile against. Published on
[NuGet.org](https://www.nuget.org/packages/HLX.GamelibGenerator) as a .NET
tool:

```
dotnet tool install -g HLX.GamelibGenerator

hlx-gamelib-generator <path/to/hlboot.dat> <output/directory>
```

Or run from source:

```
dotnet run --project src/HLX.GamelibGenerator -- <path/to/hlboot.dat> <output/directory>
```

## tests/

xUnit test projects mirroring `src/` (`HLX.Core.Tests`, `HLX.Analysis.Tests`,
`HLX.Decompiler.Tests`, `HLX.App.Tests`), plus `tests/fixtures/` holding sample
`hlboot.dat` files used across all of them.
