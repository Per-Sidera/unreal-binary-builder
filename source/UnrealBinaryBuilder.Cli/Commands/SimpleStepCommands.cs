using System.CommandLine;
using System.IO;
using UnrealBinaryBuilder.Core.Build;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Tools;

namespace UnrealBinaryBuilder.Cli.Commands;

internal static class SetupCommand
{
	public static Command Create()
	{
		var cmd = new Command("setup", "Run Setup.bat with the current settings.");
		cmd.AddOption(SharedOptions.Engine);
		cmd.AddOption(SharedOptions.Verbose);
		cmd.AddOption(SharedOptions.Quiet);
		var settingsPath = new Option<FileInfo?>(new[] { "--settings", "-s" }, "Settings.json path.");
		cmd.AddOption(settingsPath);

		cmd.SetHandler(async (ctx) =>
		{
			var dir = ctx.ParseResult.GetValueForOption(SharedOptions.Engine)!;
			var sFile = ctx.ParseResult.GetValueForOption(settingsPath);
			var verbose = ctx.ParseResult.GetValueForOption(SharedOptions.Verbose);
			var quiet = ctx.ParseResult.GetValueForOption(SharedOptions.Quiet);

			var s = BuildCommand.LoadSettings(sFile);
			s.EngineRootPath = dir.FullName;

			var logger = SharedOptions.Logger(verbose, quiet);
			var runner = new ProcessRunner(logger);
			var result = await runner.RunAsync(new ProcessOptions(
				FileName: Path.Combine(dir.FullName, EngineDetector.SetupBatFileName),
				Arguments: CommandLineBuilder.BuildSetupArgs(s),
				WorkingDirectory: dir.FullName), ctx.GetCancellationToken());
			ctx.ExitCode = result.ExitCode;
		});

		return cmd;
	}
}

internal static class GenerateProjectFilesCommand
{
	public static Command Create()
	{
		var cmd = new Command("generate-projects", "Run GenerateProjectFiles.bat.");
		cmd.AddOption(SharedOptions.Engine);
		cmd.AddOption(SharedOptions.Verbose);
		cmd.AddOption(SharedOptions.Quiet);

		cmd.SetHandler(async (ctx) =>
		{
			var dir = ctx.ParseResult.GetValueForOption(SharedOptions.Engine)!;
			var verbose = ctx.ParseResult.GetValueForOption(SharedOptions.Verbose);
			var quiet = ctx.ParseResult.GetValueForOption(SharedOptions.Quiet);

			var logger = SharedOptions.Logger(verbose, quiet);
			var runner = new ProcessRunner(logger);
			var result = await runner.RunAsync(new ProcessOptions(
				FileName: Path.Combine(dir.FullName, EngineDetector.GenerateProjectFilesBatFileName),
				Arguments: string.Empty,
				WorkingDirectory: dir.FullName), ctx.GetCancellationToken());
			ctx.ExitCode = result.ExitCode;
		});

		return cmd;
	}
}

internal static class AutomationToolCommand
{
	public static Command Create()
	{
		var cmd = new Command("build-automation-tool", "Build AutomationTool / AutomationToolLauncher.");
		cmd.AddOption(SharedOptions.Engine);
		cmd.AddOption(SharedOptions.Verbose);
		cmd.AddOption(SharedOptions.Quiet);

		cmd.SetHandler(async (ctx) =>
		{
			var dir = ctx.ParseResult.GetValueForOption(SharedOptions.Engine)!;
			var verbose = ctx.ParseResult.GetValueForOption(SharedOptions.Verbose);
			var quiet = ctx.ParseResult.GetValueForOption(SharedOptions.Quiet);

			var version = EngineDetector.ReadEngineVersion(dir.FullName);
			var logger = SharedOptions.Logger(verbose, quiet);
			var runner = new ProcessRunner(logger);

			ProcessOptions options;
			if (version?.IsUnreal5 == true)
			{
				string? msbuild = VsWhere.FindMsBuildExe();
				if (msbuild is null)
				{
					Console.Error.WriteLine("MSBuild not found via vswhere.");
					ctx.ExitCode = 2;
					return;
				}
				options = new ProcessOptions(
					FileName: msbuild,
					Arguments: $"/restore /verbosity:minimal \"{EngineDetector.AutomationToolProjectFile(dir.FullName)}\"",
					WorkingDirectory: dir.FullName);
			}
			else
			{
				options = new ProcessOptions(
					FileName: EngineDetector.RunUatBat(dir.FullName),
					Arguments: "-compileonly",
					WorkingDirectory: dir.FullName);
			}

			var result = await runner.RunAsync(options, ctx.GetCancellationToken());
			ctx.ExitCode = result.ExitCode;
		});

		return cmd;
	}
}

internal static class EngineCommand
{
	public static Command Create()
	{
		var cmd = new Command("engine", "Run the BuildGraph stage only (assumes Setup/GenerateProjects/AutomationTool already done).");
		cmd.AddOption(SharedOptions.Engine);
		cmd.AddOption(SharedOptions.Verbose);
		cmd.AddOption(SharedOptions.Quiet);
		var settingsPath = new Option<FileInfo?>(new[] { "--settings", "-s" }, "Settings.json path.");
		cmd.AddOption(settingsPath);

		cmd.SetHandler(async (ctx) =>
		{
			var dir = ctx.ParseResult.GetValueForOption(SharedOptions.Engine)!;
			var sFile = ctx.ParseResult.GetValueForOption(settingsPath);
			var verbose = ctx.ParseResult.GetValueForOption(SharedOptions.Verbose);
			var quiet = ctx.ParseResult.GetValueForOption(SharedOptions.Quiet);

			var s = BuildCommand.LoadSettings(sFile);
			s.EngineRootPath = dir.FullName;

			var logger = SharedOptions.Logger(verbose, quiet);
			var version = EngineDetector.ReadEngineVersion(dir.FullName);
			var exe = EngineDetector.FindAutomationToolExe(dir.FullName, version);
			if (exe is null || !File.Exists(exe))
			{
				Console.Error.WriteLine("AutomationTool executable not found. Build it first with `ubb build-automation-tool`.");
				ctx.ExitCode = 2;
				return;
			}

			var runner = new ProcessRunner(logger);
			var result = await runner.RunAsync(new ProcessOptions(
				FileName: exe,
				Arguments: CommandLineBuilder.BuildBuildGraphArgs(s, version),
				WorkingDirectory: dir.FullName), ctx.GetCancellationToken());
			ctx.ExitCode = result.ExitCode;
		});

		return cmd;
	}
}
