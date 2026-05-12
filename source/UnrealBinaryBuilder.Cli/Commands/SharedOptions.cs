using System.CommandLine;
using UnrealBinaryBuilder.Core.Logging;

namespace UnrealBinaryBuilder.Cli.Commands;

internal static class SharedOptions
{
	public static Option<DirectoryInfo> Engine { get; } = new(
		new[] { "--engine", "-e" },
		"Path to the Unreal Engine root (where Setup.bat lives).")
	{ IsRequired = true, ArgumentHelpName = "DIR" };

	public static Option<bool> Verbose { get; } = new(
		new[] { "--verbose", "-v" },
		"Show debug-level log output.");

	public static Option<bool> Quiet { get; } = new(
		new[] { "--quiet", "-q" },
		"Show only warnings and errors.");

	public static IBuildLogger Logger(bool verbose, bool quiet)
	{
		var min = verbose ? LogLevel.Debug : (quiet ? LogLevel.Warning : LogLevel.Info);
		return new ConsoleLogger(min);
	}
}
