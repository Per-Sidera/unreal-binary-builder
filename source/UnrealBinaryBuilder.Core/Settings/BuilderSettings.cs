using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnrealBinaryBuilder.Core.Engine;

namespace UnrealBinaryBuilder.Core.Settings;

/// <summary>
/// Persisted user settings. Pure POCO — no UI references.
/// </summary>
public sealed class BuilderSettings
{
	// App
	public string Theme { get; set; } = "Dark";
	public bool CheckForUpdatesAtStartup { get; set; } = true;
	public bool ShowEngineBuildConfirmation { get; set; } = true;
	public bool ShowDdcWarning { get; set; } = true;
	public bool ShowConsoleDeprecatedMessage { get; set; } = true;

	// Paths / inputs
	public string EngineRootPath { get; set; } = string.Empty;
	public string CustomBuildXmlFile { get; set; } = string.Empty;
	public string GameConfigurations { get; set; } = "Development;Shipping";
	public string CustomBuildOptions { get; set; } = string.Empty;
	public string AnalyticsOverride { get; set; } = string.Empty;

	// Setup.bat / git deps
	public bool GitDependencyAll { get; set; } = true;
	public List<GitDependencyPlatform> GitDependencyPlatforms { get; set; } = new()
	{
		new("Win64", true),
		new("Linux", false),
		new("Android", false),
		new("Mac", false),
		new("IOS", false),
		new("TVOS", false),
		new("HoloLens", false),
	};
	public int GitDependencyThreads { get; set; } = 4;
	public int GitDependencyMaxRetries { get; set; } = 4;
	public string GitDependencyProxy { get; set; } = string.Empty;
	public bool GitDependencyEnableCache { get; set; } = true;
	public string GitDependencyCachePath { get; set; } = string.Empty;
	public double GitDependencyCacheMultiplier { get; set; } = 2.0;
	public int GitDependencyCacheDays { get; set; } = 7;

	// Compile pipeline toggles
	public bool RunSetup { get; set; } = true;
	public bool RunGenerateProjectFiles { get; set; } = true;
	public bool RunBuildAutomationTool { get; set; } = true;
	public bool ContinueToEngineBuild { get; set; } = true;

	// Target platforms
	public bool WithWin64 { get; set; } = true;
	public bool WithWin32 { get; set; }
	public bool WithMac { get; set; }
	public bool WithLinux { get; set; }
	public bool WithLinuxArm64 { get; set; }
	public bool WithAndroid { get; set; }
	public bool WithIOS { get; set; }
	public bool WithTVOS { get; set; }
	public bool WithHoloLens { get; set; }
	public bool WithSwitch { get; set; }
	public bool WithPS4 { get; set; }
	public bool WithPS5 { get; set; }
	public bool WithXboxOne { get; set; }
	public bool WithXSX { get; set; }
	public bool HostPlatformOnly { get; set; }
	public bool HostPlatformEditorOnly { get; set; }

	// Compile options
	public bool WithDdc { get; set; } = true;
	public bool HostPlatformDdcOnly { get; set; } = true;
	public bool SignExecutables { get; set; }
	public bool EnableSymStore { get; set; }
	public bool WithFullDebugInfo { get; set; }
	public bool CleanBuild { get; set; }
	public bool WithServer { get; set; }
	public bool WithClient { get; set; }
	public bool CompileDatasmithPlugins { get; set; }

	// Shutdown after build
	public bool ShutdownPc { get; set; }
	public bool ShutdownIfBuildSuccessOnly { get; set; }

	// Engine registration
	public bool WriteRegisterEngineScript { get; set; } = true;
	public string RegisterEngineName { get; set; } = string.Empty;

	// Zip
	public bool ZipEngineBuild { get; set; }
	public bool ZipIncludePdb { get; set; } = true;
	public bool ZipIncludeDebug { get; set; }
	public bool ZipIncludeDocumentation { get; set; } = true;
	public bool ZipIncludeExtras { get; set; } = true;
	public bool ZipIncludeSource { get; set; } = true;
	public bool ZipIncludeFeaturePacks { get; set; } = true;
	public bool ZipIncludeSamples { get; set; } = true;
	public bool ZipIncludeTemplates { get; set; } = true;
	public bool ZipFastCompression { get; set; } = true;
	public string ZipEnginePath { get; set; } = string.Empty;

	/// <summary>
	/// Overwrites every settable property on this instance with the value from
	/// <paramref name="other"/>, preserving the existing object reference so
	/// bindings against it continue to work after a reload.
	/// </summary>
	public void CopyFrom(BuilderSettings other)
	{
		if (other is null) return;
		foreach (PropertyInfo p in typeof(BuilderSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (!p.CanWrite || p.GetSetMethod() is null) continue;
			object? value = p.GetValue(other);
			p.SetValue(this, value);
		}
	}
}

public sealed record GitDependencyPlatform(string Name, bool Included)
{
	public string Name { get; set; } = Name;
	public bool Included { get; set; } = Included;
}
