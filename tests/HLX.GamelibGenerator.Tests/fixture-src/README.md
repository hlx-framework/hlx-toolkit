# Gamelib generator test fixture

A purpose-built Haxe program (this directory) compiled to `.hl` bytecode
(`../fixtures/gamelib-fixture.hl`), used ONLY by `HLX.GamelibGenerator.Tests`
to drive `ClassCollector`/`EnumCollector`/`ConstructorCollector`/
`GenericGrouping`/`HaxeTypeMapper`/`HxEmitter` against real compiled shapes.
Distinct from the shared, minimal `tests/fixtures/hlboot.dat` (a tiny
`Main.hx` used by `HLX.Core.Tests` to sanity-check the raw bytecode reader) -
this one is deliberately built to exercise every generator code path, not
just parse correctness.

This program is never *run* (no `hl` interpreter is required, only `haxe`
itself for compilation) - only statically analyzed by the generator - so
nothing here needs to be logically meaningful at runtime, only present in
the compiled type/function tables in the right shape.

## What's covered, and where

- **Plain class, root package**: `Widget.hx` - instance fields, static
  fields, instance/static methods, a recovered constructor.
- **Plain class, nested package**: `fixture/pkg/Sub.hx` - the same shapes as
  `Widget` but packaged, specifically to catch a regression of a real bug
  this generator had: `ClassCollector`'s static-companion-name computation
  used `"$" + name` unconditionally, which is only correct for a root-package
  class. A packaged class's real companion is `package.$ShortName` (e.g.
  `fixture.pkg.$Sub`), not `$package.ShortName` (`$fixture.pkg.Sub`) - the
  latter never exists in real bytecode, so the bug silently produced zero
  static members for every packaged class. `Sub` has static fields/methods
  specifically so a regression shows up as "this packaged class mysteriously
  has no static members" again.
- **Real property accessors**: `Widget.x` is `(default, set)` (only a real
  `set_x` proto compiles - `HasRealGetter` must be false, `HasRealSetter`
  true); `Widget.y` is `(get, set)` (both real). Same pair of shapes again,
  packaged and on the static side, in `Sub` (`value`/`ratio`) and `Widget`
  (`resolution`/`ratio`) respectively. Plain fields with no accessor at all
  (`Widget.plainField`/`plainStatic`, `Sub.counter`/`label`) are included
  alongside for contrast.
- **Constructor recovery** (`ConstructorCollector`):
  - Normal, unambiguous: `Widget`, `Animal`, `Dog`, `EventBus`, `Sub`, `Box`
    - all called with `new X(...)` from `Main.main`.
  - No constructor declared in source at all: `NoExplicitCtor` (extends
    `Animal`, declares no `function new()` of its own - it still gets its
    OWN synthesized constructor function, distinct from `Animal`'s, because
    it has an instance field initializer (`tag:String = "sub"`) that needs
    setting after `super(...)` runs - confirmed by inspecting the actual
    generated output: `NoExplicitCtor`'s recovered findex differs from
    `Animal`'s).
  - Zero candidate sites: `NeverInstantiated` - declared, and referenced as a
    bare TYPE (a typed `null` local in `Main.main`, so the compiler still
    types it and keeps it in the output under `-dce no`), but never actually
    `new`'d anywhere in the module. `ClassCollector` must produce no `create()`
    factory for it, silently (not a skip note - see `ClassCollector`'s own
    comment on why this isn't a regression from a previously-working state).
  - Inheritance: `Dog extends Animal`, both instantiated, `Dog`'s own
    `super(name)` call must never be confused with its own `New` (see
    `Dog.hx`'s comment).
  - **Ambiguous/conflicting candidates is NOT represented in this compiled
    fixture** - every real compiled class in a real Haxe program has exactly
    one constructor implementation, so there is no way to make ordinary
    Haxe source compile to two different Call findices paired with New for
    the same class. This path is instead covered by a synthetic, hand-built
    `HlModule` in `ConstructorCollectorTests` (no bytecode compilation
    needed - `HLX.Core`'s model types are plain public records).
- **`@:generic` monomorphization**: `Box.hx`, instantiated as `Box<Int>` and
  `Box<String>` from `Main.main`. This compiles to two distinct classes
  (`Box_Int`, `Box_String`) as expected - BUT it turns out Haxe *also* still
  emits the raw, unspecialized `Box` class itself (fields erased to
  `Dynamic`), even though nothing in this module ever references bare `Box`
  directly. That means `GenericGrouping`'s own "don't shadow a genuinely
  separate, already-real class of that exact base name" guard fires for
  real here: `Box_Int`/`Box_String` are NOT collapsed, because a real `Box`
  class already exists. This is a genuine, useful finding (and probably
  explains why the generator's own comments note "0 real collapsed groups"
  for the actual game this was built for) - the compiled fixture pins this
  real shadow-guard behavior as a regression test. The actual positive
  collapse path (two members with no shadowing "Box"-named class present)
  is instead covered by a synthetic, hand-built `List<GameClass>` in
  `GenericGroupingTests` - not reachable through a real compiled fixture at
  all, since real `@:generic` usage apparently always produces the shadow.
- **Enums**: `Direction.hx` (0-arg constructors only, two of the four -
  `East`/`West` - never actually constructed anywhere, kept in the output
  only by `-dce no`); `GameEvent.hx` (mixed 0-arg/multi-param, with
  parameter types spanning `Int`, `String`, `Float`, a nested enum type
  (`Direction`), and `Array<String>` - `ItemsCollected` is likewise never
  actually constructed, kept only by `-dce no`). `EventBus.hx` supplies a
  field typed as an enum (`lastEvent:GameEvent`), a method taking one as a
  parameter (`fire(e:GameEvent)`), and a static method returning one
  (`currentDirection():Direction`).
- **Function types**: `Widget.onClick:Void->Void` (plain callback field);
  `Widget.bind(handler:Void->(Void->Void))` (the callback's own return type
  is itself a function type - `HaxeTypeMapper.MapFunctionType`'s
  nested-parenthesization case, confirmed in the actual generated output to
  emit `() -> (() -> Void)`, not the syntactically-invalid
  `() -> () -> Void`).
- **Reference/nullable types**: `Widget.maybe(?n:Int)` -> `Null<Int>`
  (`ReferenceKind.Null`). Bonus, not explicitly required but essentially
  free once the fixture exists: `Widget.addInto(r:hl.Ref<Int>, ...)` ->
  `hl.Ref<Int>` (`ReferenceKind.Ref`), confirmed in the generated output.
- **Std/excluded-namespace field types**: `Widget.tags:Array<Int>` (resolves
  to a real, correctly-parameterized `Array<Int>` - not its own generated
  wrapper); `Widget.lookup:Map<String, Int>` (compiles to
  `haxe.ds.StringMap<Int>` under hl, whose type argument has no recoverable
  source at the bytecode level and is honestly erased to
  `haxe.ds.StringMap<Dynamic>`, confirmed in the generated output, rather
  than emitting an incomplete/non-compiling bare `haxe.ds.StringMap`).

### Deliberately not forced

A genuinely private/nested/unreferenceable type (Haxe's own
`_ModuleName.NestedType` naming) is NOT present in this fixture. Haxe 4.3.3
has no local/nested `class` declaration syntax inside a function body (only
local *functions*), and reliably forcing the anonymous-structure
`@:generic`-extension-method mangling mentioned as an alternative in the
task brief is version-fragile and not worth the reliability cost for a test
fixture that's meant to be regenerable indefinitely. `Naming.
HasUnreferenceableSegment`'s own logic (leading `_` or `$` in any dotted
segment) is instead covered exhaustively via plain string input in
`NamingTests` - it's a pure string -> bool function, so a real compiled
example adds no additional confidence over a hand-written one.

## Regenerating

```
haxe -main Main -cp tests/HLX.GamelibGenerator.Tests/fixture-src \
     -hl tests/HLX.GamelibGenerator.Tests/fixtures/gamelib-fixture.hl \
     -dce no
```

Run from the `hlx-toolkit` directory. `-dce no` is required: an unreachable
class (`NeverInstantiated`), unused enum constructors (`Direction.East`/
`West`, `GameEvent.ItemsCollected`), and (had it been forceable) an
ambiguous-constructor class would otherwise be silently stripped by dead
code elimination and never appear in the compiled bytecode at all - this
fixture is never actually *run*, only statically analyzed, so nothing here
needs to survive DCE's usual "will this ever execute" reachability
analysis, only the compiler's ordinary module-loading/typing pass (which
still requires at least one syntactic reference per type - see `Main.hx`'s
own header comment).

Do NOT commit game-derived or third-party bytecode - everything under
`fixture-src/` is original and license-clean, same rule as
`tests/fixtures/README.md`.
