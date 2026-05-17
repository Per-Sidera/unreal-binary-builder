using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnrealBinaryBuilder.Core.Logging;
using UnrealBinaryBuilder.Core.Settings;

namespace UnrealBinaryBuilder.Core.Build;

public sealed record InstallVerificationResult(bool Passed, IReadOnlyList<string> Missing, long TotalBytes);

/// <summary>
/// Sanity-checks the LocalBuilds/Engine/Windows output that BuildGraph's
/// "Make Installed Build Win64" target produces. UE BuildGraph copy nodes
/// can silently undership files (filesystem errors, missing manifest entries,
/// platform-skip bugs) while still exiting 0. This verifier turns those
/// silent partial installs into explicit pipeline failures.
///
/// The required-file list is the minimum set an artist workstation needs to
/// (a) actually run the editor and (b) have .uproject registration work via
/// Register_Engine.bat. If any of these are missing, the install is not
/// shippable regardless of BuildGraph's exit code.
/// </summary>
public static class InstallOutputVerifier
{
	// Files that must exist for the install to count as "complete enough to
	// hand off." Paths are relative to the build directory (typically
	// LocalBuilds/Engine/Windows). Forward slashes for readability — Path.Combine
	// is used at check time so they normalise on Windows.
	private static readonly string[] AlwaysRequiredWin64 =
	{
		"Engine/Binaries/Win64/UnrealEditor.exe",

		// UVS is what Register_Engine.bat invokes for /register and what
		// gives artists the right-click "Switch Unreal Engine Version"
		// shell-context menu. Without it the install registers a key but
		// nothing else works.
		"Engine/Binaries/Win64/UnrealVersionSelector.exe",

		// UBT in -rocket (installed-engine) mode refuses to compile from
		// source — it requires these precompiled rules assemblies to even
		// generate project files for a .uproject. Stock Epic-launcher
		// installs always ship these; custom UBB installs have historically
		// undershipped them.
		"Engine/Intermediate/Build/BuildRules/UE5Rules.dll",
		"Engine/Intermediate/Build/BuildRules/UE5RulesManifest.json",
		"Engine/Intermediate/Build/BuildRules/UE5ProgramRules.dll",
		"Engine/Intermediate/Build/BuildRules/UE5ProgramRulesManifest.json",
	};

	// Heuristic: a real Win64 binary engine install is ~25–35 GB without DDC,
	// ~50–70 GB with InstalledDDC. Anything under 5 GB is almost certainly a
	// truncated BuildGraph copy phase (the symptom we saw with the NTFS
	// MFT-corruption case where ~6 GB of ~30 GB got copied before the
	// enumerator aborted and the node exited 0 anyway).
	public const long MinReasonableInstallBytes = 5L * 1024 * 1024 * 1024;

	public static InstallVerificationResult Verify(string buildDirectory, BuilderSettings settings, IBuildLogger logger)
	{
		var missing = new List<string>();

		if (!Directory.Exists(buildDirectory))
		{
			missing.Add($"<build directory does not exist: {buildDirectory}>");
			return new InstallVerificationResult(false, missing, 0);
		}

		foreach (string rel in AlwaysRequiredWin64)
		{
			string abs = Path.Combine(buildDirectory, rel.Replace('/', Path.DirectorySeparatorChar));
			if (!File.Exists(abs)) missing.Add(rel);
		}

		if (settings.WriteRegisterEngineScript)
		{
			string registerPath = Path.Combine(buildDirectory, EngineRegistrationScript.FileName);
			if (!File.Exists(registerPath)) missing.Add(EngineRegistrationScript.FileName);
		}

		long totalBytes = MeasureTotalSize(buildDirectory);
		if (totalBytes < MinReasonableInstallBytes)
		{
			missing.Add($"<install size {totalBytes / (1024.0 * 1024 * 1024):F2} GB is below {MinReasonableInstallBytes / (1024.0 * 1024 * 1024):F0} GB minimum — BuildGraph copy phase likely truncated>");
		}

		bool passed = missing.Count == 0;
		if (passed)
		{
			logger.Info($"Install verification passed ({totalBytes / (1024.0 * 1024 * 1024):F2} GB, {AlwaysRequiredWin64.Length} required files present).");
		}
		else
		{
			logger.Error($"Install verification FAILED — {missing.Count} required file(s) missing from {buildDirectory}:");
			foreach (string m in missing) logger.Error($"  • {m}");
			logger.Error("BuildGraph likely under-shipped during its copy phase even though it exited 0.");
			logger.Error("This is not a UBB-side bug — re-run with --print-buildgraph-args, then bisect:");
			logger.Error("  run UBB's exact BuildGraph line manually against a clean source tree,");
			logger.Error("  then re-run with just '-Set:HostPlatformOnly=true -Set:WithDDC=false'");
			logger.Error("  and diff the two install outputs. The arg that closes the gap is the fix.");
		}

		return new InstallVerificationResult(passed, missing, totalBytes);
	}

	private static long MeasureTotalSize(string root)
	{
		long total = 0;
		try
		{
			foreach (var f in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories))
			{
				try { total += f.Length; } catch { /* unreachable file, skip */ }
			}
		}
		catch
		{
			// If enumeration itself fails (e.g. the FS corruption case), the
			// missing-files check will have already flagged the problem.
		}
		return total;
	}
}
