# Unreal Binary Builder

[![Build](https://github.com/Per-Sidera/unreal-binary-builder/actions/workflows/build.yml/badge.svg)](https://github.com/Per-Sidera/unreal-binary-builder/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)

A Windows tool that produces installed binary builds of [Unreal Engine](https://www.unrealengine.com/) from
source, and packages plugins via `RunUAT BuildPlugin`.

Supports **UE 4.21+ and UE 5.0+** (tested through **5.7.4**).

This `4.0` line is a from-scratch modernisation of an older binary-builder tool, rewritten on a modern stack:

- **.NET 10** (replaces .NET Core 3.1)
- **System.IO.Compression** instead of DotNetZip
- **System.Text.Json** instead of Newtonsoft.Json
- **NetSparkleUpdater 3.x with Ed25519** instead of DSA
- **vswhere**-based MSBuild detection (no hardcoded VS paths)
- **No telemetry** — Sentry and GameAnalytics removed entirely
- **HTML5/Lumin** dropped (gone from UE long ago); UE5-only platforms (PS5, XSX) added
- New **CLI** (`ubb`) sharing all build logic with the GUI

## Repo layout

```
<repo-root>\
├── UnrealBinaryBuilder.exe        ← GUI binary (produced by publish.ps1)
├── ubb.exe                        ← CLI binary (produced by publish.ps1)
├── publish.ps1                    ← build script
├── README.md / CHANGELOG.md / LICENSE.md
├── docs/                          ← guides for the GUI / CLI / releasing
├── examples/                      ← drop-in Settings.json files
└── source/                        ← all C# projects
    ├── UnrealBinaryBuilder/             (WPF GUI)
    ├── UnrealBinaryBuilder.Cli/         (CLI front-end)
    ├── UnrealBinaryBuilder.Core/        (shared build pipeline)
    ├── UnrealBinaryBuilderUpdater/      (NetSparkle self-updater)
    └── UnrealBinaryBuilder.sln
```

The Core library has no UI dependencies; both the GUI and CLI sit on top of it.

## Quick start (GUI)

1. Grab `UnrealBinaryBuilder.exe` from a release, or build it yourself (see below).
2. Run it.
3. Browse to your Unreal Engine source folder (the one containing `Setup.bat`).
4. Tweak the pipeline / platforms / zip options.
5. Click **Build Unreal Engine**. Watch the **Output** tab for live progress.

See [`docs/gui.md`](docs/gui.md) for a full tour.

## Quick start (CLI)

```pwsh
# Show installed engines and where MSBuild lives
.\ubb.exe info

# Run the full pipeline
.\ubb.exe build --engine C:\UnrealEngine

# Just the engine compile (assumes Setup/GenerateProjects already done)
.\ubb.exe engine --engine C:\UnrealEngine

# Build a single plugin
.\ubb.exe plugin --engine C:\UE_5.7 --plugin .\MyPlugin.uplugin --out .\Out --platforms Win64,Linux

# Compile + zip a binary build for handoff to artists
.\ubb.exe build --engine C:\UnrealEngine `
    --settings .\examples\artist-build.settings.json `
    --zip-out C:\Builds\UnrealEngine_Artists.zip --verbose

# Zip an already-built engine
.\ubb.exe zip --in C:\UnrealEngine\LocalBuilds\Engine\Windows --out C:\Builds\UE.zip --no-pdb --no-source
```

See [`docs/cli.md`](docs/cli.md) for every command and option, and
[`examples/reference.settings.json`](examples/reference.settings.json) for an
annotated copy of every settings field with defaults and tradeoff notes.

### Settings presets

The `examples/` folder ships drop-in `Settings.json` files you can load via
the GUI's **Import Preset…** button or the CLI's `--settings` flag:

| File | Use case |
| --- | --- |
| [`reference.settings.json`](examples/reference.settings.json) | Annotated catalog of every option with defaults |
| [`quick-smoke.settings.json`](examples/quick-smoke.settings.json) | Fastest possible build — editor-only, no DDC, no zip |
| [`dev-build.settings.json`](examples/dev-build.settings.json) | Local dev install: host platform, all targets, no zip |
| [`artist-build.settings.json`](examples/artist-build.settings.json) | Slim Win64 binary engine for handoff, smallest-size zip |
| [`multi-platform.settings.json`](examples/multi-platform.settings.json) | Win64 + Linux + Android shipping with PDBs for crash dumps |
| [`ci-headless.settings.json`](examples/ci-headless.settings.json) | Non-interactive automation, all confirmations silenced |

JSON `//` comments and trailing commas are tolerated in all of these.

## Building from source

```pwsh
# Requires the .NET 10 SDK (https://dotnet.microsoft.com/download)
git clone https://github.com/Per-Sidera/unreal-binary-builder.git
# Or for SSH: git@github.com:Per-Sidera/unreal-binary-builder.git
cd unreal-binary-builder
dotnet build source/UnrealBinaryBuilder.sln
```

To produce single-file Windows executables (no .NET runtime needed on the target machine):

```pwsh
.\publish.ps1
```

Both `UnrealBinaryBuilder.exe` and `ubb.exe` land in the repo root, and a
`UnrealBinaryBuilder-source.zip` is produced alongside.

## Settings

User settings live in `%USERPROFILE%\Documents\UnrealBinaryBuilder\Saved\Settings.json`.
The CLI uses the same file by default; pass `--settings <path>` to override.

Logs land in `%USERPROFILE%\Documents\UnrealBinaryBuilder\Logs\`.

## Notes / gotchas

- **Visual Studio 2022** (or newer with the C++ workload) is required. The tool resolves
  MSBuild via `vswhere.exe`; if `vswhere` can't find an MSBuild with the C++ workload
  installed, the AutomationTool stage will fail.
- The first sync of an engine source pulls ~30 GB of binary blobs through `Setup.bat`.
  Be patient and configure cache settings.
- A full engine binary build takes hours and a lot of RAM/disk. Plan accordingly.
- Self-updates require an Ed25519 key pair; see [`docs/release.md`](docs/release.md)
  for how to wire that up on a fork.

## License

MIT — see [`LICENSE.md`](LICENSE.md).
