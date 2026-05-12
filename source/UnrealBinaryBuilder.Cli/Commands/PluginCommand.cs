using System.CommandLine;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Plugins;
using UnrealBinaryBuilder.Core.Zip;

namespace UnrealBinaryBuilder.Cli.Commands;

internal static class PluginCommand
{
	public static Command Create()
	{
		var cmd = new Command("plugin", "Build a single plugin via RunUAT BuildPlugin.");

		var pluginOpt = new Option<FileInfo>(new[] { "--plugin", "-p" }, "Path to the .uplugin file.")
		{ IsRequired = true };
		var destOpt = new Option<DirectoryInfo>(new[] { "--out", "-o" }, "Destination directory for the built plugin.")
		{ IsRequired = true };
		var platformsOpt = new Option<string[]>(new[] { "--platforms" }, "Target platforms, comma- or space-separated (e.g. Win64,Linux).")
		{ AllowMultipleArgumentsPerToken = true };
		var zipOpt = new Option<DirectoryInfo?>(new[] { "--zip" }, "Optional: zip the plugin output to this directory after build.");
		var marketplaceOpt = new Option<bool>(new[] { "--marketplace" }, "When zipping, exclude Binaries / Intermediate (Marketplace-ready zip).");

		cmd.AddOption(SharedOptions.Engine);
		cmd.AddOption(pluginOpt);
		cmd.AddOption(destOpt);
		cmd.AddOption(platformsOpt);
		cmd.AddOption(zipOpt);
		cmd.AddOption(marketplaceOpt);
		cmd.AddOption(SharedOptions.Verbose);
		cmd.AddOption(SharedOptions.Quiet);

		cmd.SetHandler(async (ctx) =>
		{
			var engineDir = ctx.ParseResult.GetValueForOption(SharedOptions.Engine)!;
			var plugin = ctx.ParseResult.GetValueForOption(pluginOpt)!;
			var dest = ctx.ParseResult.GetValueForOption(destOpt)!;
			var platforms = ctx.ParseResult.GetValueForOption(platformsOpt) ?? Array.Empty<string>();
			var zipDir = ctx.ParseResult.GetValueForOption(zipOpt);
			var marketplace = ctx.ParseResult.GetValueForOption(marketplaceOpt);
			var verbose = ctx.ParseResult.GetValueForOption(SharedOptions.Verbose);
			var quiet = ctx.ParseResult.GetValueForOption(SharedOptions.Quiet);

			var version = EngineDetector.ReadEngineVersion(engineDir.FullName);
			var engineInfo = new EngineInfo("CLI", engineDir.FullName, version, IsCustom: true, EngineAssociation: "cli");

			var logger = SharedOptions.Logger(verbose, quiet);
			var builder = new PluginBuilder(logger);

			// Allow comma-split inputs as well.
			var flatPlatforms = platforms
				.SelectMany(p => p.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				.ToList();

			var request = new PluginBuildRequest(
				PluginFilePath: plugin.FullName,
				DestinationDirectory: dest.FullName,
				TargetEngine: engineInfo,
				TargetPlatforms: flatPlatforms.Count > 0 ? flatPlatforms : null);

			Directory.CreateDirectory(dest.FullName);
			var result = await builder.BuildAsync(request, ctx.GetCancellationToken());

			if (result.Success && zipDir is not null)
			{
				Directory.CreateDirectory(zipDir.FullName);
				string zipPath = Path.Combine(zipDir.FullName,
					$"{Path.GetFileNameWithoutExtension(plugin.FullName)}_{version}.zip");
				var zipper = new ZipBuilder(logger);
				await zipper.ZipPluginAsync(dest.FullName, zipPath, new PluginZipOptions(marketplace));
			}

			ctx.ExitCode = result.Success ? 0 : 1;
		});

		return cmd;
	}
}
