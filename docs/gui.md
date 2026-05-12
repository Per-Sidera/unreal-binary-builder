# GUI guide

The GUI is a WPF app split into three tabs plus a status bar.

## Engine tab

This is where you run a full or partial engine build.

### Engine root
- **Path** + **Browse** — select the directory containing `Setup.bat` and `GenerateProjectFiles.bat`.
- The detected version (e.g. `UE 5.7.4`) and current commit shows automatically once a valid path is set.

### Pipeline
Four checkboxes that map 1:1 to stages of the build:

1. **Run Setup.bat** — fetches the binary dependency blobs.
2. **Run GenerateProjectFiles.bat** — generates the C# / VS project files.
3. **Build AutomationTool** — compiles AutomationTool (UE5) or AutomationToolLauncher (UE4).
4. **Continue to Engine BuildGraph** — runs the actual `Make Installed Build Win64` BuildGraph step.

Uncheck stages you've already completed to skip them.

### Setup.bat / Git dependencies
Mirrors `Setup.bat`'s flags:

- **Sync all platforms** → `--all`
- **Threads / Retries** → `--threads=N --max-retries=N`
- **Cache** → `--cache=PATH --cache-days=N --cache-size-multiplier=X`
- Excluded platforms are emitted as `--exclude=Win32` etc.

### Compile options
The flags that go into BuildGraph's `-set:` overrides:

- **Build DDC / Host DDC only** — produce a stand-alone derived data cache.
- **Sign executables** — code-sign on platforms that support it.
- **Symbol store** — include source-server data for symbol upload.
- **Full debug info** — generate full PDBs/dbg files.
- **Clean previous** — appends `-Clean` so BuildGraph wipes intermediate state.
- **Server / Client target** — builds dedicated server / client targets.
- **Datasmith plugins** — only relevant on 4.25+.

### Target platforms
- **Host platform only** disables the rest. Otherwise pick exactly the targets you need.
- Platforms unsupported by the detected engine version are still toggleable but the
  flag is dropped from the command line. (E.g. `Win32` only emits on UE4 builds.)

### Custom BuildGraph script
Override the default `Engine/Build/InstalledEngineBuild.xml` with your own. When set,
**Extra command-line options** are appended verbatim.

### Zip output
After a successful build, optionally compress the installed output to a single zip:

- Use the per-folder/per-extension toggles to keep the zip small (skip `*.pdb`,
  `Source/`, `Documentation/`, etc.).
- **Fast (lower compression)** uses `CompressionLevel.Fastest`. Otherwise
  `SmallestSize` is used (much slower, much smaller).
- Live progress is shown beneath the toggles.

### Register_Engine.bat
After a successful engine compile (and before zipping) the builder writes
`Register_Engine.bat` into `LocalBuilds\Engine\Windows\`. Artists run it after
extracting the zip — it adds the build under
`HKCU\Software\Epic Games\Unreal Engine\Builds` and calls
`UnrealVersionSelector.exe /register` so the engine appears in the
"Switch Unreal Engine Version" picker. The registry name comes from the
`RegisterEngineName` setting (defaults to `UnrealEngine_<Major>_<Minor>_<Patch>`);
`WriteRegisterEngineScript` disables the step. No admin required.

### After build
- **Shut down PC** → `shutdown /s /t 30` once compilation completes.
- **Only if successful** gates the shutdown on a successful build.

### Build button
- **Build Unreal Engine** kicks off the pipeline.
- **Cancel** kills the running stage.

## Plugins tab

Queue and build packaged plugins via `RunUAT BuildPlugin`.

1. Pick an installed engine from the dropdown (read from the Windows registry —
   `HKLM\Software\EpicGames\Unreal Engine` and `HKCU\Software\Epic Games\Unreal Engine\Builds`).
2. Pick the `.uplugin` file and a destination directory.
3. (Optional) Override the plugin's allowed platforms.
4. (Optional) Auto-zip the plugin to a location after a successful build, with a
   "Marketplace zip" toggle that drops `Binaries/` and `Intermediate/` from the archive.
5. Click **Add to queue**, then **Build queue** to compile each card sequentially.

Each card shows live state: Pending → Building → Succeeded / Failed.

## Output tab

A scrolling log of everything every stage prints. Auto-scrolls when you're at the
bottom; stops auto-scrolling if you scroll up. Each line is colour-coded:

- White — info
- Cyan — debug
- Yellow — warning
- Red — error

The full log is also saved to `…\UnrealBinaryBuilder\Logs\UnrealBinaryBuilder.log`
when the app closes.

## Status bar

Persistent at the bottom of the window:

- **Status** — what stage is running.
- **Step** — `[N/M]` from BuildGraph progress lines.
- **Compiled / Errors / Warnings** counters.
- **Elapsed** — wall-clock time of the active stage.
- **Open Logs** / **Open Settings** — shortcuts to the saved files.
- **Import Preset…** — load a settings `.json` (e.g. `examples\artist-build.settings.json`)
  and overwrite all current options in one click. Persists to the active
  `Settings.json` so the next launch picks it up.
- **Check for Updates** — pings the configured appcast.

## Settings file

Lives at `%USERPROFILE%\Documents\UnrealBinaryBuilder\Saved\Settings.json`. The
schema matches the `BuilderSettings` POCO in `UnrealBinaryBuilder.Core`. Edit by
hand if you want to share it across machines or check it into source control for
reproducible builds — the CLI accepts the same file via `--settings`, and the
GUI imports it via the **Import Preset…** button.

JSON `//` comments and trailing commas are tolerated, so the annotated
[`examples/reference.settings.json`](../examples/reference.settings.json) is
both a documentation file *and* a loadable preset. See also
[`examples/artist-build.settings.json`](../examples/artist-build.settings.json)
for a slim, opinionated Win64 artist-handoff config.

## Crash dialog

Unhandled exceptions don't ship anywhere — telemetry was removed. The crash dialog
shows the stack trace; copy it and paste into a GitHub issue if you'd like.
