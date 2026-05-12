# CLI guide

The CLI binary is **`ubb`** (ships as `ubb.exe` when self-published).

```
ubb [command] [options]
```

Run `ubb --help` to list commands, or `ubb <command> --help` for any specific
command's options.

## Common options

| Option | Description |
|---|---|
| `--engine <DIR>` (`-e`) | Path to the engine root (where `Setup.bat` lives). Required for most commands. |
| `--settings <PATH>` (`-s`) | Path to a saved `Settings.json`. Defaults to the GUI's saved settings under `Documents\UnrealBinaryBuilder\Saved\Settings.json`. |
| `--verbose` (`-v`) | Show debug log lines. |
| `--quiet` (`-q`) | Show only warnings + errors. |

## Commands

### `ubb info [--engine DIR]`

Diagnostic dump:

- Engines registered with the Launcher and as custom builds.
- MSBuild path resolved via `vswhere`.
- Latest Visual Studio install path.
- (When `--engine` is passed) Engine version, AutomationTool exe path, git branch + commit.

```
> ubb info
Installed engines:
  • 5.3                       C:\UE_5.3 (v5.3.2)
  • 5.7                       C:\UE_5.7 (v5.7.4)
  • 5.7.4 (Custom)            C:\UnrealEngine (v5.7.4)

MSBuild: C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
VS install: C:\Program Files\Microsoft Visual Studio\2022\Community
```

### `ubb build`

Run the full pipeline: Setup → GenerateProjectFiles → AutomationTool → BuildGraph.
The CLI reads your saved `Settings.json` so platform toggles and zip options come
from the GUI by default.

```
ubb build --engine C:\UnrealEngine [--settings my-settings.json]
          [--no-setup] [--no-generate-projects] [--no-automation-tool] [--no-engine]
          [--zip-out path\to\engine.zip] [--no-zip]
          [--engine-name OrbitalEngine] [--no-register-script]
```

Each `--no-*` flag turns off the matching stage even if the settings file enables it.

After a successful engine compile (and before zipping) a `Register_Engine.bat`
is dropped into `LocalBuilds\Engine\Windows\`. When an artist extracts the zip
and double-clicks the .bat it adds the engine under
`HKCU\Software\Epic Games\Unreal Engine\Builds` (no admin required) and runs
`UnrealVersionSelector.exe /register`, so the build shows up in the
"Switch Unreal Engine Version" picker. `--engine-name` controls the registry
name; `--no-register-script` disables the step.

Exit code `0` on success, `1` on any stage failure.

### `ubb setup`

Run only `Setup.bat` with arguments derived from settings (threads, retries, cache,
platform excludes…).

```
ubb setup --engine C:\UnrealEngine [--settings ...] [--verbose]
```

### `ubb generate-projects`

Run only `GenerateProjectFiles.bat`.

```
ubb generate-projects --engine C:\UnrealEngine
```

### `ubb build-automation-tool`

Compile `AutomationTool` (UE5) or `AutomationToolLauncher` (UE4). MSBuild is
located via `vswhere`.

```
ubb build-automation-tool --engine C:\UnrealEngine
```

### `ubb engine`

Run only the BuildGraph stage. Assumes `Setup.bat`, `GenerateProjectFiles.bat`,
and AutomationTool have already produced the `AutomationTool.exe` binary.

```
ubb engine --engine C:\UnrealEngine [--settings ...]
```

### `ubb plugin`

Build a single plugin via `RunUAT BuildPlugin`.

```
ubb plugin --engine C:\UE_5.7
           --plugin .\MyPlugin\MyPlugin.uplugin
           --out .\Built\MyPlugin
           [--platforms Win64,Linux,Mac]
           [--zip C:\Plugins]
           [--marketplace]
           [--verbose]
```

- `--platforms` can be repeated or comma-separated.
- `--zip` enables zipping after a successful build (one zip per plugin, named
  `<PluginName>_<EngineVersion>.zip`).
- `--marketplace` excludes `Binaries/` and `Intermediate/` from the zip.

### `ubb zip`

Standalone zipper — packages an existing installed engine output directory using
the same filtering rules as the GUI.

```
ubb zip --in C:\UnrealEngine\LocalBuilds\Engine\Windows
        --out C:\Releases\UE5.7.4.zip
        [--no-pdb] [--no-debug] [--no-source]
        [--no-docs] [--no-extras] [--no-feature-packs]
        [--no-samples] [--no-templates]
        [--fast]
```

Without `--fast` the archive uses `CompressionLevel.SmallestSize` (slow but tiny).
With `--fast` it uses `Fastest` (much faster, larger).

## CI / automation tips

- The CLI is fully non-interactive. No prompts, no GUI. Safe to run in CI.
- Cancel-friendly: `Ctrl+C` cancels via `CancellationToken`; in CI, kill the parent
  process — the runner kills its child process tree.
- Pin a `Settings.json` in your repo and pass it with `--settings` so builds are
  reproducible without depending on whoever ran the GUI last.
- Combine `ubb info --engine $ROOT` at the start of CI to log exactly which engine
  source you're about to compile.

## Examples

Full local build with a custom settings file:

```pwsh
ubb build --engine C:\UE_5.7 --settings .\release-settings.json --verbose
```

Just zip an already-built engine:

```pwsh
ubb zip --in C:\UE_5.7\LocalBuilds\Engine\Windows `
        --out D:\Builds\UE5.7.4_min.zip `
        --no-pdb --no-source --no-docs --no-feature-packs --no-samples --no-templates
```

Build the same plugin against multiple engines:

```pwsh
foreach ($engine in 'C:\UE_5.3', 'C:\UE_5.7') {
    ubb plugin --engine $engine `
               --plugin .\Plugins\ACME\ACME.uplugin `
               --out ".\Built\ACME-$([IO.Path]::GetFileName($engine))" `
               --platforms Win64,Linux `
               --zip .\Releases --marketplace
}
```
