# Releasing

## Local build

```pwsh
.\publish.ps1
```

Produces in the repo root:

- `UnrealBinaryBuilder.exe` — single-file, self-contained Windows x64 build of the
  GUI. No .NET runtime needed on the target machine.
- `ubb.exe` — single-file, self-contained Windows x64 build of the CLI.
- `UnrealBinaryBuilder-source.zip` — clean source bundle (no `bin/` / `obj/`).

## Setting up the auto-updater on a fork

The `source/UnrealBinaryBuilderUpdater` project uses
[NetSparkleUpdater](https://github.com/NetSparkleUpdater/NetSparkle) with **Ed25519**
signatures (the older DSA signer is gone).

If you're publishing your own fork, you need:

1. An `appcast.xml` URL hosted somewhere your users can reach.
2. An Ed25519 keypair — the private key signs releases; the public key is baked into
   the app to verify them.

Steps:

```pwsh
# 1. Install the NetSparkle CLI tool once.
dotnet tool install --global NetSparkleUpdater.Tools.AppCastGenerator

# 2. Generate keys.
netsparkle-generate-appcast --generate-keys

# Keys land at %localappdata%\NetSparkle. Export them with:
netsparkle-generate-appcast --export

# 3. Open source/UnrealBinaryBuilderUpdater/UnrealBinaryUpdater.cs and set:
#       UBBUpdater.AppCastXml       — your appcast URL
#       UBBUpdater.Ed25519PublicKey — the public key from --export

# 4. Each release: regenerate the appcast.
netsparkle-generate-appcast `
  --binaries . `
  --output-directory source/UnrealBinaryBuilderUpdater `
  --extension exe `
  --os windows `
  --release-notes-link https://example.com/CHANGELOG.md `
  --product-name "Unreal Binary Builder"
```

If you don't want self-update, leave `AppCastXml` empty — the GUI's update check
short-circuits and the "Check for Updates" button does nothing. Fine for personal
or internal use.

## What ships in a GitHub release

Recommended:

- `UnrealBinaryBuilder.exe` (signed if you have a code-signing cert; unsigned is
  fine — Windows SmartScreen will warn until your release builds reputation).
- `ubb.exe`
- `UnrealBinaryBuilder-source.zip`
- The current `appcast.xml` and matching `appcast.xml.signature` if you're using
  auto-update.

## Versioning

Bump `<Version>` in:

- `source/UnrealBinaryBuilder/UnrealBinaryBuilder.csproj`
- `source/UnrealBinaryBuilder.Core/UnrealBinaryBuilder.Core.csproj`
- `source/UnrealBinaryBuilder.Cli/UnrealBinaryBuilder.Cli.csproj`

… then tag the commit with the same version. The updater compares against this.
