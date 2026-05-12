# 4.0.0 (2026-05-12)

Complete modernization of the original Unreal Binary Builder. Effectively a
from-scratch rewrite on a modern stack — none of the 3.x code survives, but
the spirit of the tool (a friendly front-end to an Installed Build of UE) is
intact.

## Platform / dependencies
* .NET Core 3.1 → **.NET 10** (`net10.0-windows`).
* `Newtonsoft.Json` → `System.Text.Json` (comments + trailing commas tolerated).
* `DotNetZip` → `System.IO.Compression`.
* `NetSparkleUpdater` upgraded to 3.x; DSA → **Ed25519** signature checker.
* MSBuild detected via **vswhere.exe** (no hardcoded VS install paths).
* HandyControl 3.5.1 with a custom dark palette.
* AvalonEdit-backed log panel.
* All telemetry removed — Sentry and GameAnalytics gone.
* CefSharp embedded browser dropped (no in-app web view).

## Architecture
* Clean MVVM split: `UnrealBinaryBuilder.Core` (no UI deps), GUI, and CLI all
  sit on Core. Core is testable / scriptable in isolation.
* `BuildPipeline.RunAsync` drives every stage; both the GUI's Build button
  and `ubb build` call it identically.

## New: command-line tool `ubb`
* Same build pipeline as the GUI, exposed as `ubb {build, engine, setup,
  generate-projects, build-automation-tool, plugin, zip, info}`.
* Built on `System.CommandLine`.
* Reads the same `Settings.json` the GUI uses; `--settings <path>` to load
  any preset.
* Standard `-?` / `-h` / `--help` at every command level.

## New: settings presets in `examples/`
* `reference.settings.json` — annotated catalog of every option with defaults.
* `quick-smoke.settings.json` — fastest possible build (editor-only, no DDC).
* `dev-build.settings.json` — host-platform local install.
* `artist-build.settings.json` — Win64 binary engine for distribution.
* `multi-platform.settings.json` — Win64 + Linux + Android with PDBs.
* `ci-headless.settings.json` — non-interactive automation preset.
* GUI top-bar **Import Preset…** button loads any preset and reseats all
  bindings in-place.

## New: `Register_Engine.bat`
* After a successful engine compile (before the zip stage), a
  `Register_Engine.bat` is written into `LocalBuilds\Engine\Windows\`.
* Recipients run it to register the engine under
  `HKCU\Software\Epic Games\Unreal Engine\Builds` and invoke
  `UnrealVersionSelector.exe /register` — no admin required. The build then
  shows up in the .uproject "Switch Unreal Engine Version" picker.

## Platforms
* HTML5 dropped (gone from UE long ago).
* Lumin (Magic Leap) dropped.
* HoloLens kept for UE 4.x + UE 5.0–5.2; flagged as deprecated for UE 5.3+.
* PS5 / Xbox Series X|S added.

## Tested
* Unreal Engine **5.7.4** end-to-end build from source on Windows 11.

---

# Pre-4.0 history (Unreal-Binary-Builder by Satheesh)

# 3.1.6
**CRITICAL SECURITY UPDATE**
* [CefSharp security update.](https://github.com/ryanjon2040/Unreal-Binary-Builder/pull/58)
* HandyControl messageboxes now shows English instead of Chinese.

# 3.1.5
* **FIXED**: Crash when clicking Browse button in zip tab (reported by Gambit)

# 3.1.4

**THIS IS A CRITICAL UPDATE. DO NO SKIP**
* Updates to the updater.
* Show commit of current Engine.
* Add LinuxArm64 for Unreal Engine 5.
* Update some UE4 names to Unreal.
* Errors are now written to separate log file.
* Support custom Engines.
* New crash reporter.
* **FIXED**: Progressbar and cancel button not hiding after zipping..
* **FIXED**: Issue with CanSaveToZip.
* **FIXED**: Incorrect behavior when canceling build.
* **FIXED**: Issue when selecting Host DDC.
* **FIXED**: Plugin zipping crash if locations are same.

# 3.1.3

* Improve UE5 support
* Improve app update. Now shows changelog as well.
* Remove Engine Version selection. This is now automated.
* Remove Automation Tool Launcher selection. This is now automated.
* Use AutomationTool instead of AutomationToolLauncher for UE5.
* Improve messages in Zip tab.
* Updated dependencies.

* **FIXED**: Crashing when zipping UE5 build.
* **FIXED**: Incorrect method for OpenBuildFolder in zip tab.
* **FIXED**: Not Building Engine if all checkboxes are unchecked in Setup tab.


# 3.1.2

* **FIXED**: Issues with updating.

# 3.1.1

* Support **Unreal Engine 5**
* Add basic editor to edit target cs files.
* Add options to Start Build. You can now choose to run Setup, GenerateProjectFiles or AutomationTool.
* Add UnrealBuilderHelpers class
* New copy button in log viewer to copy message to clipboard.
* Add changelog link to menubar.
* Check and Install Update now shows version number.
* Selecting Engine Version is now optional.
* **FIXED**: Git dependency cache path
* **FIXED**: OpenClipboard Failed (0x800401D0 (CLIPBRD_E_CANT_OPEN))
* **FIXED**: ShadowErrors.cpp reporting as error.

# 3.1

* Add compiler info to plugin card.
* Improved plugin build.
* Add error message if **_RunUAT.bat_** is missing.
* **FIXED**: Update dialog not showing.
* **FIXED**: Unable to stop Engine build.
* **FIXED**: Crash if no Unreal Engine is installed.
* **FIXED**: Stop build button left disabled.
* **FIXED**: Crash if ___Resources\Icon128.png___ does not exist for plugin.
* **FIXED**: *[UBB-3]* Crash if Current Process file does not exist.
* **FIXED**: *[UBB-4]* Crash if settings file cannot be written when changing platform.
* **FIXED**: *[UBB-6]* Crash with message Input string not in correct format.

# 3.0

* Initial Release
