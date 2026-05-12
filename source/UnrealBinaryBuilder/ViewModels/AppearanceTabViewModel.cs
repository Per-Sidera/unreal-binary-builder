using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using UnrealBinaryBuilder.Core.Logging;
using UnrealBinaryBuilder.Core.Settings;
using UnrealBinaryBuilder.Theming;

namespace UnrealBinaryBuilder.ViewModels;

public sealed partial class AppearanceTabViewModel : ObservableObject
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	private readonly BuilderSettings _settings;
	private readonly LogViewModel _log;
	private bool _suppressApply;

	public ObservableCollection<string> Presets { get; }
	public ObservableCollection<ColorRoleViewModel> Roles { get; }

	[ObservableProperty] private string _selectedPreset = "Dark";

	public AppearanceTabViewModel(BuilderSettings settings, LogViewModel log)
	{
		_settings = settings;
		_log = log;

		Presets = new ObservableCollection<string>(
			ThemePalette.BuiltIn.Select(p => p.Name).Concat(new[] { "Custom" }));

		// Seed rows from whatever is currently in App.Resources (matches the
		// palette already applied on startup).
		var current = ThemeApplier.Snapshot();
		Roles = new ObservableCollection<ColorRoleViewModel>();
		foreach (var (role, label, group) in RoleLabels)
		{
			string hex = current.GetType().GetProperty(role)?.GetValue(current) as string ?? "#000000";
			var vm = new ColorRoleViewModel(role, label, group, hex);
			vm.HexChanged += OnRoleHexChanged;
			Roles.Add(vm);
		}

		_suppressApply = true;
		SelectedPreset = string.IsNullOrWhiteSpace(_settings.Theme) ? "Dark" : _settings.Theme;
		_suppressApply = false;
	}

	private static readonly (string Role, string Label, string Group)[] RoleLabels =
	{
		("Primary",         "Primary",          "Accent"),
		("DarkPrimary",     "Dark Primary",     "Accent"),
		("DarkPrimary2",    "Dark Primary 2",   "Accent"),
		("LightPrimary",    "Light Primary",    "Accent"),
		("Accent",          "Accent",           "Accent"),
		("Background",      "Background",       "Surfaces"),
		("Region",          "Region",           "Surfaces"),
		("SecondaryRegion", "Secondary Region", "Surfaces"),
		("ThirdlyRegion",   "Thirdly Region",   "Surfaces"),
		("Border",          "Border",           "Borders"),
		("SecondaryBorder", "Secondary Border", "Borders"),
		("PrimaryText",     "Primary Text",     "Text"),
		("SecondaryText",   "Secondary Text",   "Text"),
		("ThirdlyText",     "Thirdly Text",     "Text"),
		("ReverseText",     "Reverse Text",     "Text"),
		("Default",         "Default Button",   "Chrome"),
		("DarkDefault",     "Dark Default",     "Chrome"),
	};

	partial void OnSelectedPresetChanged(string value)
	{
		if (_suppressApply) return;

		ThemePalette? palette = ThemePalette.GetBuiltIn(value);
		if (palette is null)
		{
			// "Custom" — load the user's saved overrides into the rows.
			palette = ThemePalette.BuiltIn[0].Clone();
			palette.Name = "Custom";
			foreach (var kv in _settings.CustomThemeColors)
			{
				palette.GetType().GetProperty(kv.Key)?.SetValue(palette, kv.Value);
			}
		}

		ApplyPaletteToUi(palette);
		_settings.Theme = value;
	}

	private void OnRoleHexChanged(object? sender, EventArgs e)
	{
		if (_suppressApply) return;
		if (sender is not ColorRoleViewModel role) return;
		if (!ThemeApplier.TryParseHex(role.Hex, out _)) return;

		ThemeApplier.ApplyRole(role.RoleKey, role.Hex);

		// Any manual edit lifts the dropdown to "Custom" and persists the value.
		_settings.CustomThemeColors[role.RoleKey] = role.Hex;
		if (SelectedPreset != "Custom")
		{
			_suppressApply = true;
			SelectedPreset = "Custom";
			_settings.Theme = "Custom";
			_suppressApply = false;
		}
	}

	private void ApplyPaletteToUi(ThemePalette palette)
	{
		ThemeApplier.Apply(palette);

		_suppressApply = true;
		try
		{
			foreach (var row in Roles)
			{
				string? hex = palette.GetType().GetProperty(row.RoleKey)?.GetValue(palette) as string;
				if (!string.IsNullOrEmpty(hex)) row.SetHexSilently(hex);
			}
		}
		finally { _suppressApply = false; }
	}

	[RelayCommand]
	private void ResetToPreset()
	{
		var name = SelectedPreset == "Custom" ? "Dark" : SelectedPreset;
		var preset = ThemePalette.GetBuiltIn(name) ?? ThemePalette.BuiltIn[0];
		_suppressApply = true;
		SelectedPreset = preset.Name;
		_suppressApply = false;
		ApplyPaletteToUi(preset);
		_settings.Theme = preset.Name;
		_settings.CustomThemeColors.Clear();
		_log.Info($"Theme reset to {preset.Name}.");
	}

	[RelayCommand]
	private void SaveCustomPalette()
	{
		var dlg = new Microsoft.Win32.SaveFileDialog
		{
			Title = "Save palette as .json",
			Filter = "Palette JSON (*.json)|*.json",
			FileName = $"{(string.IsNullOrEmpty(SelectedPreset) ? "palette" : SelectedPreset)}.json",
		};
		if (dlg.ShowDialog() != true) return;

		var snapshot = ThemeApplier.Snapshot();
		snapshot.Name = Path.GetFileNameWithoutExtension(dlg.FileName);
		File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(snapshot, JsonOptions));
		_log.Info($"Palette saved to {dlg.FileName}");
		Growl.Success($"Saved {Path.GetFileName(dlg.FileName)}");
	}

	[RelayCommand]
	private void LoadCustomPalette()
	{
		var dlg = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Load palette .json",
			Filter = "Palette JSON (*.json)|*.json|All files (*.*)|*.*",
			CheckFileExists = true,
		};
		if (dlg.ShowDialog() != true) return;

		try
		{
			string json = File.ReadAllText(dlg.FileName);
			var loaded = JsonSerializer.Deserialize<ThemePalette>(json, JsonOptions);
			if (loaded is null)
			{
				_log.Warn($"Could not parse '{dlg.FileName}' as a palette.");
				return;
			}

			_suppressApply = true;
			SelectedPreset = "Custom";
			_settings.Theme = "Custom";
			_settings.CustomThemeColors.Clear();
			foreach (var e in loaded.Entries())
			{
				_settings.CustomThemeColors[e.Key] = e.Value;
			}
			_suppressApply = false;

			ApplyPaletteToUi(loaded);
			_log.Info($"Loaded palette {Path.GetFileName(dlg.FileName)}");
			Growl.Success($"Loaded {Path.GetFileName(dlg.FileName)}");
		}
		catch (Exception ex)
		{
			_log.Error($"Failed to load palette: {ex.Message}");
			Growl.Error("Could not load palette — see Output tab.");
		}
	}
}

public sealed partial class ColorRoleViewModel : ObservableObject
{
	public string RoleKey { get; }
	public string Label { get; }
	public string Group { get; }

	[ObservableProperty] private string _hex;
	private bool _silent;

	public event EventHandler? HexChanged;

	public ColorRoleViewModel(string roleKey, string label, string group, string hex)
	{
		RoleKey = roleKey;
		Label = label;
		Group = group;
		_silent = true;
		Hex = hex;
		_silent = false;
	}

	partial void OnHexChanged(string value)
	{
		if (_silent) return;
		HexChanged?.Invoke(this, EventArgs.Empty);
	}

	public void SetHexSilently(string newHex)
	{
		if (Hex == newHex) return;
		_silent = true;
		Hex = newHex;
		_silent = false;
	}
}
