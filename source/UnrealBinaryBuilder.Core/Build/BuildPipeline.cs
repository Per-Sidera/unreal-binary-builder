using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Logging;
using UnrealBinaryBuilder.Core.Settings;
using UnrealBinaryBuilder.Core.Tools;

namespace UnrealBinaryBuilder.Core.Build;

public enum BuildStep
{
	None,
	Setup,
	GenerateProjectFiles,
	BuildAutomationTool,
	BuildAutomationToolLauncher,
	BuildEngine
}

public sealed record BuildOutcome(BuildStep LastStep, bool Success, int Errors, int Warnings, TimeSpan Elapsed);

/// <summary>
/// Sequential pipeline: Setup.bat → GenerateProjectFiles.bat → AutomationTool[Launcher] build → BuildGraph.
/// Each stage is gated by settings. Failures short-circuit the pipeline.
/// </summary>
public sealed class BuildPipeline
{
	private readonly IBuildLogger _logger;
	private readonly IProcessProgress? _progress;

	public BuildPipeline(IBuildLogger logger, IProcessProgress? progress = null)
	{
		_logger = logger;
		_progress = progress;
	}

	public async Task<BuildOutcome> RunAsync(BuilderSettings settings, CancellationToken cancellationToken = default)
	{
		string root = settings.EngineRootPath;
		if (!EngineDetector.IsEngineRoot(root))
		{
			_logger.Error($"'{root}' is not a valid Unreal Engine root (Setup.bat / GenerateProjectFiles.bat missing).");
			return new BuildOutcome(BuildStep.None, false, 1, 0, TimeSpan.Zero);
		}

		EngineVersion? version = EngineDetector.ReadEngineVersion(root);
		if (version != null) _logger.Info($"Detected Unreal Engine {version}");

		var runner = new ProcessRunner(_logger, _progress);
		var totalSw = System.Diagnostics.Stopwatch.StartNew();
		int errors = 0, warnings = 0;
		BuildStep lastStep = BuildStep.None;

		if (settings.RunSetup)
		{
			lastStep = BuildStep.Setup;
			var setupResult = await runner.RunAsync(new ProcessOptions(
				FileName: Path.Combine(root, EngineDetector.SetupBatFileName),
				Arguments: CommandLineBuilder.BuildSetupArgs(settings),
				WorkingDirectory: root), cancellationToken);
			errors += setupResult.Errors; warnings += setupResult.Warnings;
			if (setupResult.ExitCode != 0)
			{
				return Done(lastStep, false, errors, warnings, totalSw);
			}
		}

		if (settings.RunGenerateProjectFiles)
		{
			lastStep = BuildStep.GenerateProjectFiles;
			var gpfResult = await runner.RunAsync(new ProcessOptions(
				FileName: Path.Combine(root, EngineDetector.GenerateProjectFilesBatFileName),
				Arguments: string.Empty,
				WorkingDirectory: root), cancellationToken);
			errors += gpfResult.Errors; warnings += gpfResult.Warnings;
			if (gpfResult.ExitCode != 0)
			{
				return Done(lastStep, false, errors, warnings, totalSw);
			}
		}

		if (settings.RunBuildAutomationTool)
		{
			lastStep = version?.IsUnreal5 == true
				? BuildStep.BuildAutomationTool
				: BuildStep.BuildAutomationToolLauncher;

			var atResult = await BuildAutomationToolStage(runner, root, version, cancellationToken);
			if (atResult is not null)
			{
				errors += atResult.Errors; warnings += atResult.Warnings;
				if (atResult.ExitCode != 0)
				{
					return Done(lastStep, false, errors, warnings, totalSw);
				}
			}
		}

		if (!settings.ContinueToEngineBuild)
		{
			return Done(lastStep, true, errors, warnings, totalSw);
		}

		string? automationExe = EngineDetector.FindAutomationToolExe(root, version);
		if (automationExe is null || !File.Exists(automationExe))
		{
			_logger.Error("AutomationTool executable not found. Cannot start engine build.");
			return Done(lastStep, false, errors + 1, warnings, totalSw);
		}

		lastStep = BuildStep.BuildEngine;
		string buildGraphArgs = CommandLineBuilder.BuildBuildGraphArgs(settings, version);
		var engineResult = await runner.RunAsync(new ProcessOptions(
			FileName: automationExe,
			Arguments: buildGraphArgs,
			WorkingDirectory: root), cancellationToken);
		errors += engineResult.Errors; warnings += engineResult.Warnings;

		bool engineSucceeded = engineResult.ExitCode == 0;

		if (engineSucceeded)
		{
			string? buildDir = ResolveBuildDir(root);
			if (buildDir is null)
			{
				_logger.Error("BuildGraph exited 0 but no LocalBuilds/Engine/Windows output directory exists — install did not produce anything.");
				return Done(lastStep, false, errors + 1, warnings, totalSw);
			}

			// Forensic dump: record the exact RunUAT line UBB constructed,
			// alongside the install. Lets a later operator (or another
			// agent) bisect args against a stock minimal-args invocation
			// without re-running UBB.
			TryWriteBuildGraphCommandLog(buildDir, automationExe, buildGraphArgs);

			if (settings.WriteRegisterEngineScript)
			{
				try
				{
					EngineRegistrationScript.Write(buildDir, settings.RegisterEngineName, _logger);
				}
				catch (Exception ex)
				{
					_logger.Warn($"Failed to write {EngineRegistrationScript.FileName}: {ex.Message}");
				}
			}

			// Verifier runs LAST so Register_Engine.bat presence is part of the
			// check. Failing here turns silent partial installs into explicit
			// pipeline failures — see InstallOutputVerifier for the file list
			// and rationale.
			if (settings.VerifyInstallOutput)
			{
				var verification = InstallOutputVerifier.Verify(buildDir, settings, _logger);
				if (!verification.Passed)
				{
					return Done(lastStep, false, errors + verification.Missing.Count, warnings, totalSw);
				}
			}
			else
			{
				_logger.Warn("Install verification skipped (VerifyInstallOutput=false). Output completeness is not guaranteed.");
			}
		}

		return Done(lastStep, engineSucceeded, errors, warnings, totalSw);
	}

	private void TryWriteBuildGraphCommandLog(string buildDir, string automationExe, string args)
	{
		try
		{
			string path = Path.Combine(buildDir, "_buildgraph-command.txt");
			string body = $"# Exact RunUAT invocation that produced this install.{Environment.NewLine}" +
						  $"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
						  $"# Use this to bisect args against a stock minimal-args run if the install is missing files.{Environment.NewLine}{Environment.NewLine}" +
						  $"\"{automationExe}\" {args}{Environment.NewLine}";
			File.WriteAllText(path, body);
		}
		catch (Exception ex)
		{
			_logger.Debug($"Could not write _buildgraph-command.txt: {ex.Message}");
		}
	}

	/// <summary>Locates the installed engine output directory (where InstalledEngineBuild.xml drops files).</summary>
	public static string? ResolveBuildDir(string engineRoot)
	{
		string platformDir = Path.Combine(engineRoot, "LocalBuilds", "Engine", "Windows");
		if (Directory.Exists(platformDir)) return platformDir;

		string fallback = Path.Combine(engineRoot, "LocalBuilds", "Engine");
		if (Directory.Exists(fallback)) return fallback;

		return null;
	}

	private async Task<ProcessResult?> BuildAutomationToolStage(
		ProcessRunner runner, string root, EngineVersion? version, CancellationToken cancellationToken)
	{
		// On UE5, AutomationTool builds via dotnet on the .csproj.
		// On UE4, we just run RunUAT.bat -compileonly which compiles AutomationToolLauncher.
		if (version?.IsUnreal5 == true)
		{
			string projectFile = EngineDetector.AutomationToolProjectFile(root);
			if (!File.Exists(projectFile))
			{
				_logger.Warn($"AutomationTool project file not found at {projectFile}. Skipping.");
				return null;
			}

			// Prefer the dotnet CLI: UE 5.8 targets .NET 10, which VS 2022's bundled
			// MSBuild (17.x for .NET Framework) does not understand, so calling
			// MSBuild directly on a UE 5.8 source tree errors out with NETSDK1045.
			// The dotnet CLI uses the host machine's .NET SDK installation, which
			// covers every UE5 version that ships.
			if (IsDotnetOnPath())
			{
				return await runner.RunAsync(new ProcessOptions(
					FileName: "dotnet",
					Arguments: $"build -c Development \"{projectFile}\"",
					WorkingDirectory: root), cancellationToken);
			}

			// Last-resort fallback to MSBuild from Visual Studio — only used if the
			// dotnet CLI isn't on PATH. Will fail on UE 5.8+ source trees if VS
			// hasn't been updated to a version that supports .NET 10 projects.
			string? msbuild = VsWhere.FindMsBuildExe();
			if (msbuild != null)
			{
				_logger.Warn("dotnet CLI not found on PATH; falling back to MSBuild from Visual Studio. This will fail for UE 5.8+ until VS supports .NET 10 projects.");
				return await runner.RunAsync(new ProcessOptions(
					FileName: msbuild,
					Arguments: $"/restore /verbosity:minimal \"{projectFile}\"",
					WorkingDirectory: root), cancellationToken);
			}

			_logger.Error("Neither dotnet CLI nor MSBuild was found. Install the .NET 10 SDK (or whichever SDK the engine targets).");
			return null;
		}

		// UE4 path
		string runUat = EngineDetector.RunUatBat(root);
		if (!File.Exists(runUat))
		{
			_logger.Warn("RunUAT.bat not found, skipping AutomationToolLauncher compile.");
			return null;
		}

		return await runner.RunAsync(new ProcessOptions(
			FileName: runUat,
			Arguments: "-compileonly",
			WorkingDirectory: root), cancellationToken);
	}

	private static BuildOutcome Done(BuildStep step, bool ok, int errors, int warnings, System.Diagnostics.Stopwatch sw)
	{
		sw.Stop();
		return new BuildOutcome(step, ok, errors, warnings, sw.Elapsed);
	}

	private static bool IsDotnetOnPath()
	{
		string? path = Environment.GetEnvironmentVariable("PATH");
		if (string.IsNullOrEmpty(path)) return false;
		string exeName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
		foreach (string dir in path.Split(Path.PathSeparator))
		{
			if (string.IsNullOrWhiteSpace(dir)) continue;
			try
			{
				if (File.Exists(Path.Combine(dir, exeName))) return true;
			}
			catch { /* malformed PATH entry, skip */ }
		}
		return false;
	}
}
