using System.CommandLine;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Tools;

namespace UnrealBinaryBuilder.Cli.Commands;

internal static class InfoCommand
{
	public static Command Create()
	{
		var cmd = new Command("info", "Show detected engines, MSBuild path, and engine version at a path.");
		cmd.AddOption(SharedOptions.Engine);
		SharedOptions.Engine.IsRequired = false;

		cmd.SetHandler((DirectoryInfo? dir) =>
		{
			Console.WriteLine("Installed engines:");
			var engines = EngineDetector.EnumerateInstalledEngines();
			if (engines.Count == 0)
			{
				Console.WriteLine("  (none registered)");
			}
			foreach (var e in engines)
			{
				Console.WriteLine($"  • {e.EngineName,-25} {e.EnginePath} (v{e.Version})");
			}

			Console.WriteLine();
			Console.WriteLine($"MSBuild: {VsWhere.FindMsBuildExe() ?? "(not found)"}");
			Console.WriteLine($"VS install: {VsWhere.FindLatestVisualStudioInstall() ?? "(not found)"}");

			if (dir is not null)
			{
				Console.WriteLine();
				Console.WriteLine($"Engine root: {dir.FullName}");
				Console.WriteLine($"  Is engine root:    {EngineDetector.IsEngineRoot(dir.FullName)}");
				Console.WriteLine($"  Engine version:    {EngineDetector.ReadEngineVersion(dir.FullName)?.ToString() ?? "(unknown)"}");
				Console.WriteLine($"  AutomationTool exe: {EngineDetector.FindAutomationToolExe(dir.FullName, EngineDetector.ReadEngineVersion(dir.FullName)) ?? "(not built)"}");
				var git = GitInfo.Read(dir.FullName);
				if (git is not null) Console.WriteLine($"  Git: {git.Branch} @ {git.ShortSha}");
			}
		}, SharedOptions.Engine);

		return cmd;
	}
}
