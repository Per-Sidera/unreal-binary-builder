using System.CommandLine;
using UnrealBinaryBuilder.Cli.Commands;

namespace UnrealBinaryBuilder.Cli;

public static class Program
{
	public static async Task<int> Main(string[] args)
	{
		var root = new RootCommand("Unreal Binary Builder — build the Unreal Engine and plugins from source.");
		root.AddCommand(BuildCommand.Create());
		root.AddCommand(SetupCommand.Create());
		root.AddCommand(GenerateProjectFilesCommand.Create());
		root.AddCommand(AutomationToolCommand.Create());
		root.AddCommand(EngineCommand.Create());
		root.AddCommand(PluginCommand.Create());
		root.AddCommand(ZipCommand.Create());
		root.AddCommand(InfoCommand.Create());

		return await root.InvokeAsync(args);
	}
}
