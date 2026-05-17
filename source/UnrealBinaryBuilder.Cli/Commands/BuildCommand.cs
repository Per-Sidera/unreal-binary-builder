using System.CommandLine;
using UnrealBinaryBuilder.Core.Build;
using UnrealBinaryBuilder.Core.Logging;
using UnrealBinaryBuilder.Core.Settings;
using UnrealBinaryBuilder.Core.Zip;

namespace UnrealBinaryBuilder.Cli.Commands;

internal static class BuildCommand
{
	public static Command Create()
	{
		var cmd = new Command("build", "Run the full pipeline: Setup → GenerateProjectFiles → AutomationTool → BuildGraph (and optionally zip).");
		cmd.AddOption(SharedOptions.Engine);
		cmd.AddOption(SharedOptions.Verbose);
		cmd.AddOption(SharedOptions.Quiet);

		var settingsPath = new Option<FileInfo?>(
			new[] { "--settings", "-s" },
			"Path to a saved Settings.json file (defaults to the GUI's saved settings).");
		var skipSetup = new Option<bool>("--no-setup", "Skip running Setup.bat.");
		var skipGpf = new Option<bool>("--no-generate-projects", "Skip running GenerateProjectFiles.bat.");
		var skipAt = new Option<bool>("--no-automation-tool", "Skip building AutomationTool.");
		var skipEngine = new Option<bool>("--no-engine", "Skip the BuildGraph engine compile.");
		var zipOut = new Option<FileInfo?>(
			new[] { "--zip-out", "-z" },
			"Path to the zip file to produce after a successful build. Overrides any path in settings and turns zipping on.");
		var noZip = new Option<bool>("--no-zip", "Skip the zip stage even if settings have it enabled.");
		var engineName = new Option<string?>(
			new[] { "--engine-name", "-n" },
			"Name used for the Register_Engine.bat script bundled with the build (HKCU\\Software\\Epic Games\\Unreal Engine\\Builds key). Defaults to UnrealEngine_<Major>_<Minor>_<Patch>.");
		var noRegisterScript = new Option<bool>("--no-register-script", "Do not write Register_Engine.bat into the build output.");
		var noVerify = new Option<bool>("--no-verify", "Skip the post-build install-output verifier. Don't use this for shipped builds — it exists for diagnosing the verifier itself or recovering from a known-partial run.");
		var printBuildGraphArgs = new Option<bool>("--print-buildgraph-args", "Print the BuildGraph command line UBB would invoke for these settings (with the resolved engine version) and exit. Runs no build. Use this to bisect args against a stock minimal-args invocation when an install is undershipping.");

		cmd.AddOption(settingsPath);
		cmd.AddOption(skipSetup);
		cmd.AddOption(skipGpf);
		cmd.AddOption(skipAt);
		cmd.AddOption(skipEngine);
		cmd.AddOption(zipOut);
		cmd.AddOption(noZip);
		cmd.AddOption(engineName);
		cmd.AddOption(noRegisterScript);
		cmd.AddOption(noVerify);
		cmd.AddOption(printBuildGraphArgs);

		cmd.SetHandler(async (ctx) =>
		{
			var dir = ctx.ParseResult.GetValueForOption(SharedOptions.Engine)!;
			bool verbose = ctx.ParseResult.GetValueForOption(SharedOptions.Verbose);
			bool quiet = ctx.ParseResult.GetValueForOption(SharedOptions.Quiet);
			var sFile = ctx.ParseResult.GetValueForOption(settingsPath);
			var zipOutValue = ctx.ParseResult.GetValueForOption(zipOut);
			var noZipValue = ctx.ParseResult.GetValueForOption(noZip);

			BuilderSettings settings = LoadSettings(sFile);
			settings.EngineRootPath = dir.FullName;
			settings.RunSetup = settings.RunSetup && !ctx.ParseResult.GetValueForOption(skipSetup);
			settings.RunGenerateProjectFiles = settings.RunGenerateProjectFiles && !ctx.ParseResult.GetValueForOption(skipGpf);
			settings.RunBuildAutomationTool = settings.RunBuildAutomationTool && !ctx.ParseResult.GetValueForOption(skipAt);
			settings.ContinueToEngineBuild = settings.ContinueToEngineBuild && !ctx.ParseResult.GetValueForOption(skipEngine);

			if (zipOutValue is not null)
			{
				settings.ZipEngineBuild = true;
				settings.ZipEnginePath = zipOutValue.FullName;
			}
			if (noZipValue)
			{
				settings.ZipEngineBuild = false;
			}

			string? engineNameValue = ctx.ParseResult.GetValueForOption(engineName);
			if (!string.IsNullOrWhiteSpace(engineNameValue))
			{
				settings.RegisterEngineName = engineNameValue;
			}
			if (ctx.ParseResult.GetValueForOption(noRegisterScript))
			{
				settings.WriteRegisterEngineScript = false;
			}
			if (ctx.ParseResult.GetValueForOption(noVerify))
			{
				settings.VerifyInstallOutput = false;
			}

			if (ctx.ParseResult.GetValueForOption(printBuildGraphArgs))
			{
				var version = UnrealBinaryBuilder.Core.Engine.EngineDetector.ReadEngineVersion(settings.EngineRootPath);
				string args = UnrealBinaryBuilder.Core.Build.CommandLineBuilder.BuildBuildGraphArgs(settings, version);
				Console.WriteLine(args);
				ctx.ExitCode = 0;
				return;
			}

			var logger = SharedOptions.Logger(verbose, quiet);
			var pipeline = new BuildPipeline(logger);
			var outcome = await pipeline.RunAsync(settings, ctx.GetCancellationToken());
			if (!outcome.Success)
			{
				ctx.ExitCode = 1;
				return;
			}

			if (settings.ZipEngineBuild && !string.IsNullOrEmpty(settings.ZipEnginePath))
			{
				ctx.ExitCode = await ZipEngineOutputAsync(settings, logger, ctx.GetCancellationToken());
				return;
			}

			ctx.ExitCode = 0;
		});

		return cmd;
	}

	private static async Task<int> ZipEngineOutputAsync(BuilderSettings settings, IBuildLogger logger, CancellationToken cancellationToken)
	{
		string? buildDir = BuildPipeline.ResolveBuildDir(settings.EngineRootPath);
		if (buildDir is null)
		{
			logger.Error($"Could not find installed engine output under {settings.EngineRootPath}\\LocalBuilds.");
			return 1;
		}

		logger.Info($"Zipping {buildDir} → {settings.ZipEnginePath}");
		try
		{
			var zipper = new ZipBuilder(logger);
			await zipper.ZipEngineBuildAsync(buildDir, settings.ZipEnginePath, EngineZipOptions.FromSettings(settings), cancellationToken);
			return 0;
		}
		catch (OperationCanceledException)
		{
			logger.Warn("Zip canceled.");
			return 130;
		}
		catch (Exception ex)
		{
			logger.Error($"Zipping failed: {ex.Message}");
			return 1;
		}
	}

	internal static BuilderSettings LoadSettings(FileInfo? settingsFile)
	{
		if (settingsFile is { Exists: true })
		{
			string json = File.ReadAllText(settingsFile.FullName);
			return System.Text.Json.JsonSerializer.Deserialize<BuilderSettings>(json, SettingsStore.JsonOptions)
				?? new BuilderSettings();
		}

		return new SettingsStore().Load();
	}
}
