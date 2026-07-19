# Test Fixtures

Place a `hlboot.dat` here compiled from a minimal, license-clean Haxe program.

Example `Main.hx`:

```haxe
class Main {
    static function main() {
        trace("hlx fixture");
    }
}
```

Compile with:

```
haxe -main Main -hl hlboot.dat
```

Do NOT commit game-derived or third-party bytecode.
