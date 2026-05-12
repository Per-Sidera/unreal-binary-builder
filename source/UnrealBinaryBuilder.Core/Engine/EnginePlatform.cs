namespace UnrealBinaryBuilder.Core.Engine;

/// <summary>
/// Target platforms recognised by the binary build pipeline.
/// HTML5 (gone since 4.24) and Lumin (gone since 4.27) are deliberately omitted.
/// Win32 was dropped in UE5 but is still valid on UE4.x.
/// HoloLens was dropped in 5.3 but still valid on earlier 5.x and 4.x.
/// </summary>
public enum EnginePlatform
{
	Win64,
	Win32,
	Linux,
	LinuxArm64,
	Mac,
	IOS,
	TVOS,
	Android,
	HoloLens,
	Switch,
	PS4,
	PS5,
	XboxOne,
	XSX
}

public static class EnginePlatformExtensions
{
	public static string ToBuildGraphFlag(this EnginePlatform p) => p switch
	{
		EnginePlatform.Win64 => "WithWin64",
		EnginePlatform.Win32 => "WithWin32",
		EnginePlatform.Linux => "WithLinux",
		EnginePlatform.LinuxArm64 => "WithLinuxArm64",
		EnginePlatform.Mac => "WithMac",
		EnginePlatform.IOS => "WithIOS",
		EnginePlatform.TVOS => "WithTVOS",
		EnginePlatform.Android => "WithAndroid",
		EnginePlatform.HoloLens => "WithHoloLens",
		EnginePlatform.Switch => "WithSwitch",
		EnginePlatform.PS4 => "WithPS4",
		EnginePlatform.PS5 => "WithPS5",
		EnginePlatform.XboxOne => "WithXboxOne",
		EnginePlatform.XSX => "WithXSX",
		_ => p.ToString()
	};

	public static bool IsSupportedBy(this EnginePlatform p, EngineVersion? v)
	{
		if (v is null) return true;
		return p switch
		{
			EnginePlatform.Win32 => v.IsUnreal4,
			EnginePlatform.LinuxArm64 => v.IsUnreal5 || v.AsDouble >= 4.24,
			EnginePlatform.HoloLens => v.IsUnreal4 || (v.Major == 5 && v.Minor < 3),
			EnginePlatform.PS5 or EnginePlatform.XSX => v.IsUnreal5,
			_ => true
		};
	}
}
