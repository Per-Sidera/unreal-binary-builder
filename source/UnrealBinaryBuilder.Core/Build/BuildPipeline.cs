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
		var engineResult = await runner.RunAsync(new ProcessOptions(
			FileName: automationExe,
			Arguments: CommandLineBuilder.BuildBuildGraphArgs(settings, version),
			WorkingDirectory: root), cancellationToken);
		errors += engineResult.Errors; warnings += engineResult.Warnings;

		bool engineSucceeded = engineResult.ExitCode == 0;
		if (engineSucceeded && settings.WriteRegisterEngineScript)
		{
			try
			{
				string? buildDir = ResolveBuildDir(root);
				if (buildDir is not null)
				{
					EngineRegistrationScript.Write(buildDir, settings.RegisterEngineName, _logger);
				}
				else
				{
					_logger.Warn($"Skipped writing {EngineRegistrationScript.FileName}: installed engine output directory not found.");
				}
			}
			catch (Exception ex)
			{
				_logger.Warn($"Failed to write {EngineRegistrationScript.FileName}: {ex.Message}");
			}
		}

		return Done(lastStep, engineSucceeded, errors, warnings, totalSw);
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
		// On UE5, AutomationTool builds via dotnet/MSBuild on the .csproj.
		// On UE4, we just run RunUAT.bat -compileonly which compiles AutomationToolLauncher.
		if (version?.IsUnreal5 == true)
		{
			string projectFile = EngineDetector.AutomationToolProjectFile(root);
			if (!File.Exists(projectFile))
			{
				_logger.Warn($"AutomationTool project file not found at {projectFile}. Skipping.");
				return null;
			}

			string? msbuild = VsWhere.FindMsBuildExe();
			if (msbuild != null)
			{
				return await runner.RunAsync(new ProcessOptions(
					FileName: msbuild,
					Arguments: $"/restore /verbosity:minimal \"{projectFile}\"",
					WorkingDirectory: root), cancellationToken);
			}

			// Fallback to dotnet build (UE5 AutomationTool now targets .NET — works in newer engines).
			return await runner.RunAsync(new ProcessOptions(
				FileName: "dotnet",
				Arguments: $"build -c Development \"{projectFile}\"",
				WorkingDirectory: root), cancellationToken);
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
}
