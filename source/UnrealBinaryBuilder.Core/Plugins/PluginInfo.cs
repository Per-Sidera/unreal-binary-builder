using System.IO;
using System.Text.Json;

namespace UnrealBinaryBuilder.Core.Plugins;

public sealed record PluginInfo(
	string FriendlyName,
	string Description,
	string PluginFilePath,
	IReadOnlyList<string>? AllowedPlatforms);

public static class PluginManifestReader
{
	public static PluginInfo? Read(string upluginPath)
	{
		if (!File.Exists(upluginPath)) return null;
		try
		{
			using var stream = File.OpenRead(upluginPath);
			using var doc = JsonDocument.Parse(stream);

			string friendly = doc.RootElement.TryGetProperty("FriendlyName", out var fn)
				? fn.GetString() ?? Path.GetFileNameWithoutExtension(upluginPath)
				: Path.GetFileNameWithoutExtension(upluginPath);

			string desc = doc.RootElement.TryGetProperty("Description", out var d)
				? d.GetString() ?? string.Empty
				: string.Empty;

			List<string>? platforms = null;
			if (doc.RootElement.TryGetProperty("Modules", out var modules) && modules.ValueKind == JsonValueKind.Array)
			{
				foreach (var module in modules.EnumerateArray())
				{
					// Newer field name is PlatformAllowList; legacy field is WhitelistPlatforms.
					if (TryReadStringArray(module, "PlatformAllowList", out var newer)) { platforms = newer; break; }
					if (TryReadStringArray(module, "WhitelistPlatforms", out var legacy)) { platforms = legacy; break; }
				}
			}

			return new PluginInfo(friendly, desc, upluginPath, platforms);
		}
		catch
		{
			return null;
		}
	}

	private static bool TryReadStringArray(JsonElement obj, string property, out List<string>? values)
	{
		values = null;
		if (!obj.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array) return false;
		values = new List<string>();
		foreach (var v in arr.EnumerateArray())
		{
			if (v.ValueKind == JsonValueKind.String)
			{
				string? s = v.GetString();
				if (!string.IsNullOrEmpty(s)) values.Add(s);
			}
		}
		return values.Count > 0;
	}
}
