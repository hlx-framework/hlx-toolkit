# Changelog

All notable changes to hlx-toolkit's publishable artifacts are documented in
this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

`HLX.App`, `HLX.Viewer`, and `HLX.GamelibGenerator` each version and release
independently (see each project's `<Version>`). Entries are grouped by
artifact, then by version.

Add entries under the relevant artifact's `[Unreleased]` section as changes
land. Before tagging a release, move that artifact's `[Unreleased]` entries
under a new `[X.Y.Z] - YYYY-MM-DD` heading, and bump `<Version>` in that
artifact's `.csproj` to match the tag (e.g. `HLX.App/X.Y.Z`) — the
corresponding `publish-*.yml` workflow checks this and fails otherwise.

Shared libraries (`HLX.Core`, `HLX.Analysis`, `HLX.Decompiler`) don't release
independently.

## HLX.App

### [Unreleased]

### [0.0.1] - 2026-07-19

- Initial release: Avalonia desktop GUI for browsing HashLink bytecode - tree
  view of types/functions/globals, search, find-usages, and decompiled-source
  view.

## HLX.Viewer

### [Unreleased]

### [0.0.1] - 2026-07-19

- Initial release: scriptable CLI inspector for HashLink bytecode - dumps
  types, functions, disassembly, and classes from a `.hl`/`hlboot.dat` file.

## HLX.GamelibGenerator

### [Unreleased]

### [0.0.1] - 2026-07-19

- Initial release: offline CLI that reads a game's `hlboot.dat` and emits
  typed Haxe gamelib wrappers (one `abstract` per game class) for mods to
  compile against.
