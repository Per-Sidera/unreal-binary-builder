using System.Text;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Settings;

namespace UnrealBinaryBuilder.Core.Build;

/// <summary>Translates settings into the command-line arguments understood by Setup.bat / AutomationTool.</summary>
public static class CommandLineBuilder
{
	public static string BuildSetupArgs(BuilderSettings s)
	{
		var sb = new StringBuilder("--force");

		if (s.GitDependencyAll)
		{
			sb.Append(" --all");
		}

		foreach (var p in s.GitDependencyPlatforms)
		{
			if (!p.Included)
			{
				sb.Append(" --exclude=").Append(p.Name);
			}
		}

		sb.Append(" --threads=").Append(s.GitDependencyThreads);
		sb.Append(" --max-retries=").Append(s.GitDependencyMaxRetries);

		if (!s.GitDependencyEnableCache)
		{
			sb.Append(" --no-cache");
		}
		else if (!string.IsNullOrEmpty(s.GitDependencyCachePath))
		{
			sb.Append(" --cache=").Append(s.GitDependencyCachePath.Replace('\\', '/'));
			sb.Append(" --cache-size-multiplier=").Append(s.GitDependencyCacheMultiplier);
			sb.Append(" --cache-days=").Append(s.GitDependencyCacheDays);
		}

		if (!string.IsNullOrEmpty(s.GitDependencyProxy))
		{
			sb.Append(" --proxy=").Append(s.GitDependencyProxy);
		}

		return sb.ToString();
	}

	public static string BuildBuildGraphArgs(BuilderSettings s, EngineVersion? version)
	{
		string buildXml = string.IsNullOrEmpty(s.CustomBuildXmlFile)
			? EngineDetector.DefaultBuildXmlFile
			: s.CustomBuildXmlFile;
		string buildXmlArg = buildXml == EngineDetector.DefaultBuildXmlFile ? buildXml : $"\"{buildXml}\"";

		string gameConfigs = string.IsNullOrEmpty(s.GameConfigurations) ? "Development;Shipping" : s.GameConfigurations;

		var sb = new StringBuilder();
		sb.Append($"BuildGraph -target=\"Make Installed Build Win64\" -script={buildXmlArg}");
		sb.Append($" -set:WithDDC={B(s.WithDdc)}");
		sb.Append($" -set:SignExecutables={B(s.SignExecutables)}");
		sb.Append($" -set:EmbedSrcSrvInfo={B(s.EnableSymStore)}");
		sb.Append($" -set:GameConfigurations={gameConfigs}");
		sb.Append($" -set:WithFullDebugInfo={B(s.WithFullDebugInfo)}");
		sb.Append($" -set:HostPlatformEditorOnly={B(s.HostPlatformEditorOnly)}");
		if (!string.IsNullOrEmpty(s.AnalyticsOverride))
		{
			sb.Append($" -set:AnalyticsTypeOverride={s.AnalyticsOverride}");
		}

		if (s.WithDdc && s.HostPlatformDdcOnly)
		{
			sb.Append(" -set:HostPlatformDDCOnly=true");
		}

		if (s.HostPlatformOnly)
		{
			sb.Append(" -set:HostPlatformOnly=true");
		}
		else
		{
			AppendIfSupported(sb, version, EnginePlatform.Win64, s.WithWin64);
			AppendIfSupported(sb, version, EnginePlatform.Win32, s.WithWin32);
			AppendIfSupported(sb, version, EnginePlatform.Mac, s.WithMac);
			AppendIfSupported(sb, version, EnginePlatform.Linux, s.WithLinux);
			AppendIfSupported(sb, version, EnginePlatform.LinuxArm64, s.WithLinuxArm64);
			AppendIfSupported(sb, version, EnginePlatform.Android, s.WithAndroid);
			AppendIfSupported(sb, version, EnginePlatform.IOS, s.WithIOS);
			AppendIfSupported(sb, version, EnginePlatform.TVOS, s.WithTVOS);
			AppendIfSupported(sb, version, EnginePlatform.HoloLens, s.WithHoloLens);
			AppendIfSupported(sb, version, EnginePlatform.Switch, s.WithSwitch);
			AppendIfSupported(sb, version, EnginePlatform.PS4, s.WithPS4);
			AppendIfSupported(sb, version, EnginePlatform.PS5, s.WithPS5);
			AppendIfSupported(sb, version, EnginePlatform.XboxOne, s.WithXboxOne);
			AppendIfSupported(sb, version, EnginePlatform.XSX, s.WithXSX);
		}

		// Datasmith was added in 4.25.
		if (version is not null && version.AsDouble >= 4.25)
		{
			sb.Append($" -set:CompileDatasmithPlugins={B(s.CompileDatasmithPlugins)}");
		}

		// Server/Client targets were added in 4.23.
		if (version is null || version.AsDouble > 4.22)
		{
			sb.Append($" -set:WithServer={B(s.WithServer)}");
			sb.Append($" -set:WithClient={B(s.WithClient)}");
		}

		if (!string.IsNullOrEmpty(s.CustomBuildOptions) && buildXml != EngineDetector.DefaultBuildXmlFile)
		{
			sb.Append(' ').Append(s.CustomBuildOptions);
		}

		if (s.CleanBuild)
		{
			sb.Append(" -Clean");
		}

		return sb.ToString();
	}

	private static void AppendIfSupported(StringBuilder sb, EngineVersion? v, EnginePlatform p, bool enabled)
	{
		if (!p.IsSupportedBy(v)) return;
		sb.Append($" -set:{p.ToBuildGraphFlag()}={B(enabled)}");
	}

	private static string B(bool b) => b ? "true" : "false";
}
