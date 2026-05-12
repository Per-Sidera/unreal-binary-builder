using System.CommandLine;
using System.IO.Compression;
using UnrealBinaryBuilder.Core.Zip;

namespace UnrealBinaryBuilder.Cli.Commands;

internal static class ZipCommand
{
	public static Command Create()
	{
		var cmd = new Command("zip", "Zip an installed engine output directory with the standard filters.");
		var inDir = new Option<DirectoryInfo>(new[] { "--in", "-i" }, "Installed engine directory (typically LocalBuilds/Engine/Windows).")
		{ IsRequired = true };
		var outFile = new Option<FileInfo>(new[] { "--out", "-o" }, "Output zip path.")
		{ IsRequired = true };
		var fast = new Option<bool>("--fast", "Use fastest compression (default: smallest).");
		var skipPdb = new Option<bool>("--no-pdb", "Skip *.pdb.");
		var skipDebug = new Option<bool>("--no-debug", "Skip *.debug.");
		var skipSource = new Option<bool>("--no-source", "Skip Engine/Source directories.");
		var skipDoc = new Option<bool>("--no-docs", "Skip Documentation/.");
		var skipExtras = new Option<bool>("--no-extras", "Skip Extras/ (excluding Redist).");
		var skipFeaturePacks = new Option<bool>("--no-feature-packs", "Skip FeaturePacks/.");
		var skipSamples = new Option<bool>("--no-samples", "Skip Samples/.");
		var skipTemplates = new Option<bool>("--no-templates", "Skip Templates/.");

		cmd.AddOption(inDir);
		cmd.AddOption(outFile);
		cmd.AddOption(fast);
		cmd.AddOption(skipPdb);
		cmd.AddOption(skipDebug);
		cmd.AddOption(skipSource);
		cmd.AddOption(skipDoc);
		cmd.AddOption(skipExtras);
		cmd.AddOption(skipFeaturePacks);
		cmd.AddOption(skipSamples);
		cmd.AddOption(skipTemplates);
		cmd.AddOption(SharedOptions.Verbose);
		cmd.AddOption(SharedOptions.Quiet);

		cmd.SetHandler(async (ctx) =>
		{
			var src = ctx.ParseResult.GetValueForOption(inDir)!;
			var dst = ctx.ParseResult.GetValueForOption(outFile)!;
			var verbose = ctx.ParseResult.GetValueForOption(SharedOptions.Verbose);
			var quiet = ctx.ParseResult.GetValueForOption(SharedOptions.Quiet);

			var options = new EngineZipOptions(
				IncludePdb: !ctx.ParseResult.GetValueForOption(skipPdb),
				IncludeDebug: !ctx.ParseResult.GetValueForOption(skipDebug),
				IncludeDocumentation: !ctx.ParseResult.GetValueForOption(skipDoc),
				IncludeExtras: !ctx.ParseResult.GetValueForOption(skipExtras),
				IncludeSource: !ctx.ParseResult.GetValueForOption(skipSource),
				IncludeFeaturePacks: !ctx.ParseResult.GetValueForOption(skipFeaturePacks),
				IncludeSamples: !ctx.ParseResult.GetValueForOption(skipSamples),
				IncludeTemplates: !ctx.ParseResult.GetValueForOption(skipTemplates),
				CompressionLevel: ctx.ParseResult.GetValueForOption(fast) ? CompressionLevel.Fastest : CompressionLevel.SmallestSize);

			var logger = SharedOptions.Logger(verbose, quiet);
			var zipper = new ZipBuilder(logger);
			await zipper.ZipEngineBuildAsync(src.FullName, dst.FullName, options, ctx.GetCancellationToken());
			ctx.ExitCode = 0;
		});

		return cmd;
	}
}
