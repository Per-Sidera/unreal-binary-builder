using System.IO;
using Microsoft.Win32;

namespace UnrealBinaryBuilder.Core.Engine;

public static class EngineDetector
{
	public const string SetupBatFileName = "Setup.bat";
	public const string GenerateProjectFilesBatFileName = "GenerateProjectFiles.bat";
	public const string AutomationToolName = "AutomationTool";
	public const string AutomationToolLauncherName = "AutomationToolLauncher";
	public const string DefaultBuildXmlFile = "Engine/Build/InstalledEngineBuild.xml";

	public static bool IsEngineRoot(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) return false;
		return File.Exists(Path.Combine(path, SetupBatFileName))
			&& File.Exists(Path.Combine(path, GenerateProjectFilesBatFileName));
	}

	public static EngineVersion? ReadEngineVersion(string baseEnginePath)
	{
		if (string.IsNullOrWhiteSpace(baseEnginePath)) return null;
		string versionFile = Path.Combine(baseEnginePath, "Engine", "Source", "Runtime", "Launch", "Resources", "Version.h");
		if (!File.Exists(versionFile)) return null;

		int? major = null, minor = null, patch = null;
		foreach (string raw in File.ReadAllLines(versionFile))
		{
			string line = raw.Trim();
			if (TryParseDefine(line, "ENGINE_MAJOR_VERSION", out int v)) major = v;
			else if (TryParseDefine(line, "ENGINE_MINOR_VERSION", out v)) minor = v;
			else if (TryParseDefine(line, "ENGINE_PATCH_VERSION", out v)) { patch = v; break; }
		}

		if (major is null || minor is null) return null;
		return new EngineVersion(major.Value, minor.Value, patch ?? 0);
	}

	private static bool TryParseDefine(string line, string name, out int value)
	{
		value = 0;
		string token = $"#define {name}";
		int idx = line.IndexOf(token, StringComparison.Ordinal);
		if (idx < 0) return false;
		string rest = line[(idx + token.Length)..].Trim();
		return int.TryParse(rest, out value);
	}

	public static string? FindAutomationToolExe(string baseEnginePath, EngineVersion? version)
	{
		if (string.IsNullOrWhiteSpace(baseEnginePath)) return null;

		// UE5 layout: Engine/Binaries/DotNET/AutomationTool/AutomationTool.exe
		string ue5 = Path.Combine(baseEnginePath, "Engine", "Binaries", "DotNET",
			AutomationToolName, $"{AutomationToolName}.exe");
		if (File.Exists(ue5)) return ue5;

		// UE4 layout: Engine/Binaries/DotNET/AutomationToolLauncher.exe
		string ue4 = Path.Combine(baseEnginePath, "Engine", "Binaries", "DotNET",
			$"{AutomationToolLauncherName}.exe");
		if (File.Exists(ue4)) return ue4;

		// Either may be the expected location based on version even if not yet built.
		if (version?.IsUnreal5 == true) return ue5;
		if (version?.IsUnreal4 == true) return ue4;

		return ue5; // default to UE5 path
	}

	public static string AutomationToolProjectFile(string baseEnginePath)
		=> Path.Combine(baseEnginePath, "Engine", "Source", "Programs", AutomationToolName, $"{AutomationToolName}.csproj");

	public static string AutomationToolLauncherProjectFile(string baseEnginePath)
		=> Path.Combine(baseEnginePath, "Engine", "Source", "Programs", AutomationToolLauncherName, $"{AutomationToolLauncherName}.csproj");

	public static string RunUatBat(string baseEnginePath)
		=> Path.Combine(baseEnginePath, "Engine", "Build", "BatchFiles", "RunUAT.bat");

	/// <summary>Reads installed engine versions from the Windows registry (Launcher + custom builds).</summary>
	public static IReadOnlyList<EngineInfo> EnumerateInstalledEngines()
	{
		var result = new List<EngineInfo>();

		try
		{
			using var launcherKey = Registry.LocalMachine.OpenSubKey(@"Software\EpicGames\Unreal Engine");
			if (launcherKey != null)
			{
				foreach (string subKeyName in launcherKey.GetSubKeyNames())
				{
					using var sub = launcherKey.OpenSubKey(subKeyName);
					if (sub?.GetValue("InstalledDirectory") is string path && Directory.Exists(path))
					{
						var version = ReadEngineVersion(path);
						result.Add(new EngineInfo(subKeyName, path, version, IsCustom: false, EngineAssociation: subKeyName));
					}
				}
			}
		}
		catch { /* registry not available */ }

		try
		{
			using var customKey = Registry.CurrentUser.OpenSubKey(@"Software\Epic Games\Unreal Engine\Builds");
			if (customKey != null)
			{
				foreach (string name in customKey.GetValueNames())
				{
					if (customKey.GetValue(name) is string path && Directory.Exists(path))
					{
						var version = ReadEngineVersion(path);
						string display = version != null ? $"{version} (Custom)" : $"{name} (Custom)";
						result.Add(new EngineInfo(display, path, version, IsCustom: true, EngineAssociation: name));
					}
				}
			}
		}
		catch { /* */ }

		return result;
	}
}
