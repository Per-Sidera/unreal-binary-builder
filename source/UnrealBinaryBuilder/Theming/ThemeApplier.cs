using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace UnrealBinaryBuilder.Theming;

/// <summary>
/// Pushes a <see cref="ThemePalette"/> into Application.Current.Resources at
/// runtime. The brushes registered in App.xaml are kept as the same instances
/// so DynamicResource consumers update live; we just rewrite the .Color
/// property on each brush (and the underlying Color resource for any consumer
/// reading the Color directly).
/// </summary>
public static class ThemeApplier
{
	// Resource-key name in App.xaml for each palette role. Order matters only
	// for the role label table in the UI; the dictionary itself is keyed.
	private static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>
	{
		["Primary"]         = "PrimaryColor",
		["DarkPrimary"]     = "DarkPrimaryColor",
		["DarkPrimary2"]    = "DarkPrimary2Color",
		["LightPrimary"]    = "LightPrimaryColor",
		["Accent"]          = "AccentColor",
		["Background"]      = "BackgroundColor",
		["Region"]          = "RegionColor",
		["SecondaryRegion"] = "SecondaryRegionColor",
		["ThirdlyRegion"]   = "ThirdlyRegionColor",
		["Border"]          = "BorderColor",
		["SecondaryBorder"] = "SecondaryBorderColor",
		["PrimaryText"]     = "PrimaryTextColor",
		["SecondaryText"]   = "SecondaryTextColor",
		["ThirdlyText"]     = "ThirdlyTextColor",
		["ReverseText"]     = "ReverseTextColor",
		["Default"]         = "DefaultColor",
		["DarkDefault"]     = "DarkDefaultColor",
	};

	// Brush resource keys (in App.xaml) keyed by colour role.
	private static readonly IReadOnlyDictionary<string, string[]> Brushes = new Dictionary<string, string[]>
	{
		["Primary"]         = new[] { "PrimaryBrush" },
		["DarkPrimary"]     = new[] { "DarkPrimaryBrush" },
		["DarkPrimary2"]    = new[] { "DarkPrimary2Brush" },
		["LightPrimary"]    = new[] { "LightPrimaryBrush" },
		["Accent"]          = new[] { "AccentBrush" },
		["Background"]      = new[] { "BackgroundBrush", "MainContentBackgroundBrush" },
		["Region"]          = new[] { "RegionBrush", "TitleBrush" },
		["SecondaryRegion"] = new[] { "SecondaryRegionBrush" },
		["ThirdlyRegion"]   = new[] { "ThirdlyRegionBrush" },
		["Border"]          = new[] { "BorderBrush" },
		["SecondaryBorder"] = new[] { "SecondaryBorderBrush" },
		["PrimaryText"]     = new[] { "PrimaryTextBrush", "TextIconBrush" },
		["SecondaryText"]   = new[] { "SecondaryTextBrush" },
		["ThirdlyText"]     = new[] { "ThirdlyTextBrush" },
		["ReverseText"]     = new[] { "ReverseTextBrush" },
		["Default"]         = new[] { "DefaultBrush" },
		["DarkDefault"]     = new[] { "DarkDefaultBrush" },
	};

	public static IReadOnlyDictionary<string, string> RoleResourceKeys => Roles;

	public static void Apply(ThemePalette palette)
	{
		if (Application.Current is null) return;

		foreach (var entry in palette.Entries())
		{
			ApplyRole(entry.Key, entry.Value);
		}
	}

	public static void ApplyRole(string role, string hex)
	{
		if (!Roles.TryGetValue(role, out var colorKey)) return;
		if (!TryParseHex(hex, out var color)) return;

		var res = Application.Current.Resources;
		res[colorKey] = color;

		if (Brushes.TryGetValue(role, out var brushKeys))
		{
			foreach (var brushKey in brushKeys)
			{
				if (res[brushKey] is SolidColorBrush brush && !brush.IsFrozen)
				{
					brush.Color = color;
				}
				else
				{
					// Frozen brush (e.g. inherited from HC defaults) — replace the
					// entry. DynamicResource consumers will re-resolve to the new
					// instance.
					res[brushKey] = new SolidColorBrush(color);
				}
			}
		}
	}

	public static bool TryParseHex(string hex, out Color color)
	{
		color = default;
		if (string.IsNullOrWhiteSpace(hex)) return false;
		try
		{
			object? parsed = ColorConverter.ConvertFromString(hex.Trim());
			if (parsed is Color c) { color = c; return true; }
		}
		catch { }
		return false;
	}

	/// <summary>Reads the current Application resources into a palette so the UI can edit them.</summary>
	public static ThemePalette Snapshot()
	{
		var p = new ThemePalette { Name = "Custom" };
		foreach (var role in Roles.Keys)
		{
			var hex = ReadRole(role);
			SetRole(p, role, hex);
		}
		return p;
	}

	private static string ReadRole(string role)
	{
		if (!Roles.TryGetValue(role, out var key)) return "#000000";
		if (Application.Current?.Resources[key] is Color c)
		{
			return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
		}
		return "#000000";
	}

	private static void SetRole(ThemePalette p, string role, string hex)
	{
		switch (role)
		{
			case "Primary":         p.Primary = hex;         break;
			case "DarkPrimary":     p.DarkPrimary = hex;     break;
			case "DarkPrimary2":    p.DarkPrimary2 = hex;    break;
			case "LightPrimary":    p.LightPrimary = hex;    break;
			case "Accent":          p.Accent = hex;          break;
			case "Background":      p.Background = hex;      break;
			case "Region":          p.Region = hex;          break;
			case "SecondaryRegion": p.SecondaryRegion = hex; break;
			case "ThirdlyRegion":   p.ThirdlyRegion = hex;   break;
			case "Border":          p.Border = hex;          break;
			case "SecondaryBorder": p.SecondaryBorder = hex; break;
			case "PrimaryText":     p.PrimaryText = hex;     break;
			case "SecondaryText":   p.SecondaryText = hex;   break;
			case "ThirdlyText":     p.ThirdlyText = hex;     break;
			case "ReverseText":     p.ReverseText = hex;     break;
			case "Default":         p.Default = hex;         break;
			case "DarkDefault":     p.DarkDefault = hex;     break;
		}
	}
}
