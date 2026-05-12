using System.Reflection;

namespace UnrealBinaryBuilder.Helpers;

public static class AppInfo
{
	public static string Version => GetVersion();

	private static string GetVersion()
	{
		var v = Assembly.GetExecutingAssembly().GetName().Version;
		if (v is null) return "0.0.0";
		string s = $"{v.Major}.{v.Minor}";
		if (v.Build > 0) s += $".{v.Build}";
		if (v.Revision > 0) s += $".{v.Revision}";
		return s;
	}
}
