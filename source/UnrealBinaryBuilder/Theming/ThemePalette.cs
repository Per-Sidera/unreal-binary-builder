using System.Collections.Generic;
using System.Linq;

namespace UnrealBinaryBuilder.Theming;

/// <summary>
/// The 17 colour roles that drive the GUI's HandyControl theme. Every
/// editable role lives here as a hex string ("#RRGGBB"). The applier maps
/// these onto Application.Current.Resources so DynamicResource consumers
/// pick the new values up live, without an app restart.
/// </summary>
public sealed class ThemePalette
{
	public string Name { get; set; } = "Custom";

	// Primary / accent
	public string Primary       { get; set; } = "#3DBEB0";
	public string DarkPrimary   { get; set; } = "#2EA095";
	public string DarkPrimary2  { get; set; } = "#249088";
	public string LightPrimary  { get; set; } = "#4FD0C2";
	public string Accent        { get; set; } = "#3DBEB0";

	// Surfaces
	public string Background        { get; set; } = "#1B1C1F";
	public string Region            { get; set; } = "#222327";
	public string SecondaryRegion   { get; set; } = "#2A2B30";
	public string ThirdlyRegion     { get; set; } = "#33343A";

	// Borders
	public string Border            { get; set; } = "#3A3B41";
	public string SecondaryBorder   { get; set; } = "#2F3035";

	// Text
	public string PrimaryText       { get; set; } = "#ECECEC";
	public string SecondaryText     { get; set; } = "#B5B6BB";
	public string ThirdlyText       { get; set; } = "#7E7F84";
	public string ReverseText       { get; set; } = "#1B1C1F";

	// Default buttons / chrome
	public string Default           { get; set; } = "#2A2B30";
	public string DarkDefault       { get; set; } = "#33343A";

	public ThemePalette Clone() => new()
	{
		Name = Name,
		Primary = Primary, DarkPrimary = DarkPrimary, DarkPrimary2 = DarkPrimary2,
		LightPrimary = LightPrimary, Accent = Accent,
		Background = Background, Region = Region,
		SecondaryRegion = SecondaryRegion, ThirdlyRegion = ThirdlyRegion,
		Border = Border, SecondaryBorder = SecondaryBorder,
		PrimaryText = PrimaryText, SecondaryText = SecondaryText,
		ThirdlyText = ThirdlyText, ReverseText = ReverseText,
		Default = Default, DarkDefault = DarkDefault,
	};

	public static IReadOnlyList<ThemePalette> BuiltIn { get; } = new[]
	{
		new ThemePalette
		{
			Name = "Dark",
			Primary = "#3DBEB0", DarkPrimary = "#2EA095", DarkPrimary2 = "#249088",
			LightPrimary = "#4FD0C2", Accent = "#3DBEB0",
			Background = "#1B1C1F", Region = "#222327",
			SecondaryRegion = "#2A2B30", ThirdlyRegion = "#33343A",
			Border = "#3A3B41", SecondaryBorder = "#2F3035",
			PrimaryText = "#ECECEC", SecondaryText = "#B5B6BB",
			ThirdlyText = "#7E7F84", ReverseText = "#1B1C1F",
			Default = "#2A2B30", DarkDefault = "#33343A",
		},
		new ThemePalette
		{
			Name = "Light",
			Primary = "#2E9C90", DarkPrimary = "#247A71", DarkPrimary2 = "#1B635B",
			LightPrimary = "#54C5B6", Accent = "#2E9C90",
			Background = "#F5F5F7", Region = "#FFFFFF",
			SecondaryRegion = "#EDEDF1", ThirdlyRegion = "#E0E0E5",
			Border = "#C9C9CF", SecondaryBorder = "#DADAE0",
			PrimaryText = "#1B1C1F", SecondaryText = "#52535A",
			ThirdlyText = "#8A8B91", ReverseText = "#FFFFFF",
			Default = "#EDEDF1", DarkDefault = "#DCDCE3",
		},
		new ThemePalette
		{
			Name = "Solarized Dark",
			Primary = "#268BD2", DarkPrimary = "#1E6FA9", DarkPrimary2 = "#185988",
			LightPrimary = "#4FA5DC", Accent = "#B58900",
			Background = "#002B36", Region = "#073642",
			SecondaryRegion = "#0B4453", ThirdlyRegion = "#10546A",
			Border = "#586E75", SecondaryBorder = "#0B4453",
			PrimaryText = "#FDF6E3", SecondaryText = "#93A1A1",
			ThirdlyText = "#657B83", ReverseText = "#002B36",
			Default = "#073642", DarkDefault = "#10546A",
		},
		new ThemePalette
		{
			Name = "Nord",
			Primary = "#88C0D0", DarkPrimary = "#6A9FB2", DarkPrimary2 = "#557F8E",
			LightPrimary = "#A3D2DE", Accent = "#BF616A",
			Background = "#2E3440", Region = "#3B4252",
			SecondaryRegion = "#434C5E", ThirdlyRegion = "#4C566A",
			Border = "#4C566A", SecondaryBorder = "#3B4252",
			PrimaryText = "#ECEFF4", SecondaryText = "#D8DEE9",
			ThirdlyText = "#A6ACBA", ReverseText = "#2E3440",
			Default = "#3B4252", DarkDefault = "#4C566A",
		},
		new ThemePalette
		{
			Name = "Catppuccin Mocha",
			Primary = "#CBA6F7", DarkPrimary = "#A186CB", DarkPrimary2 = "#806AA3",
			LightPrimary = "#D8BFFA", Accent = "#F38BA8",
			Background = "#1E1E2E", Region = "#181825",
			SecondaryRegion = "#313244", ThirdlyRegion = "#45475A",
			Border = "#45475A", SecondaryBorder = "#313244",
			PrimaryText = "#CDD6F4", SecondaryText = "#BAC2DE",
			ThirdlyText = "#7F849C", ReverseText = "#1E1E2E",
			Default = "#313244", DarkDefault = "#45475A",
		},
	};

	public static ThemePalette? GetBuiltIn(string name)
		=> BuiltIn.FirstOrDefault(p => string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase));

	/// <summary>Returns the 17 (role-key, current-hex) pairs in display order.</summary>
	public IEnumerable<KeyValuePair<string, string>> Entries()
	{
		yield return new("Primary",          Primary);
		yield return new("DarkPrimary",      DarkPrimary);
		yield return new("DarkPrimary2",     DarkPrimary2);
		yield return new("LightPrimary",     LightPrimary);
		yield return new("Accent",           Accent);
		yield return new("Background",       Background);
		yield return new("Region",           Region);
		yield return new("SecondaryRegion",  SecondaryRegion);
		yield return new("ThirdlyRegion",    ThirdlyRegion);
		yield return new("Border",           Border);
		yield return new("SecondaryBorder",  SecondaryBorder);
		yield return new("PrimaryText",      PrimaryText);
		yield return new("SecondaryText",    SecondaryText);
		yield return new("ThirdlyText",      ThirdlyText);
		yield return new("ReverseText",      ReverseText);
		yield return new("Default",          Default);
		yield return new("DarkDefault",      DarkDefault);
	}
}
