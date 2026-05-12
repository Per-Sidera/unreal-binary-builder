using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using UnrealBinaryBuilder.Core.Logging;
using UnrealBinaryBuilder.Core.Settings;
using UnrealBinaryBuilder.Helpers;
using UnrealBinaryBuilderUpdater;

namespace UnrealBinaryBuilder.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
	public SettingsStore SettingsStore { get; }
	public BuilderSettings Settings { get; }
	public LogViewModel Log { get; } = new();
	public StatusViewModel Status { get; } = new();
	public ZipProgressViewModel ZipProgress { get; } = new();
	public EngineTabViewModel Engine { get; }
	public PluginsTabViewModel Plugins { get; }

	[ObservableProperty] private string _versionLabel = $"v{AppInfo.Version}";
	[ObservableProperty] private string _updateButtonContent = "Check for Updates";
	[ObservableProperty] private bool _updateAvailable;

	// True when the updater has an appcast URL wired up. While false, the Check
	// for Updates button stays hidden — there's nothing useful to do.
	public bool IsUpdaterConfigured => UBBUpdater.IsConfigured;

	private static UBBUpdater? _updater;

	public MainViewModel()
	{
		SettingsStore = new SettingsStore();
		Settings = SettingsStore.Load();
		Engine = new EngineTabViewModel(Settings, Log, Status, ZipProgress);
		Plugins = new PluginsTabViewModel(Log, Status);

		Log.Info($"Welcome to Unreal Binary Builder {AppInfo.Version}.");
		if (Settings.CheckForUpdatesAtStartup)
		{
			_ = CheckForUpdatesSilentlyAsync();
		}
	}

	[RelayCommand]
	private void OpenLogFolder()
	{
		try
		{
			Process.Start("explorer.exe", SettingsStore.LogDir);
		}
		catch (Exception ex)
		{
			Log.Error($"Failed to open log folder: {ex.Message}");
		}
	}

	[RelayCommand]
	private void OpenSettingsFile()
	{
		try
		{
			Process.Start("notepad.exe", SettingsStore.SettingsPath);
		}
		catch (Exception ex)
		{
			Log.Error($"Failed to open settings: {ex.Message}");
		}
	}

	[RelayCommand]
	private void ImportSettings()
	{
		var dlg = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Import settings preset",
			Filter = "Settings JSON (*.json)|*.json|All files (*.*)|*.*",
			CheckFileExists = true
		};
		if (dlg.ShowDialog() != true) return;

		try
		{
			string json = File.ReadAllText(dlg.FileName);
			var loaded = JsonSerializer.Deserialize<BuilderSettings>(json, SettingsStore.JsonOptions);
			if (loaded is null)
			{
				Log.Warn($"Could not parse '{dlg.FileName}' as a settings file.");
				return;
			}

			Settings.CopyFrom(loaded);
			Engine.OnSettingsReloaded();
			SaveSettings();

			Log.Info($"Loaded settings preset from {dlg.FileName}");
			Growl.Success($"Loaded preset: {Path.GetFileName(dlg.FileName)}");
		}
		catch (Exception ex)
		{
			Log.Error($"Failed to import settings: {ex.Message}");
			Growl.Error("Could not import settings — see Output tab.");
		}
	}

	[RelayCommand]
	private void OpenChangelog()
	{
		// Local CHANGELOG.md if it lives next to the exe; otherwise no-op.
		string local = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
		if (File.Exists(local))
		{
			OpenUrl(local);
		}
	}

	private static void OpenUrl(string url)
	{
		Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
	}

	[RelayCommand]
	private void CheckForUpdates()
	{
		_ = CheckForUpdatesSilentlyAsync();
	}

	private Task CheckForUpdatesSilentlyAsync()
	{
		// Silent skip — no log noise — while no appcast is wired up.
		if (!UBBUpdater.IsConfigured) return Task.CompletedTask;
		try
		{
			_updater ??= new UBBUpdater();
			_updater.SilentUpdateFinishedEventHandler += OnUpdateChecked;
			_updater.CheckForUpdatesSilently();
		}
		catch (Exception ex)
		{
			Log.Warn($"Update check failed: {ex.Message}");
		}
		return Task.CompletedTask;
	}

	private void OnUpdateChecked(object? sender, UpdateProgressFinishedEventArgs e)
	{
		Application.Current.Dispatcher.BeginInvoke(() =>
		{
			switch (e.appUpdateCheckStatus)
			{
				case AppUpdateCheckStatus.UpdateAvailable:
					UpdateAvailable = true;
					UpdateButtonContent = $"Update {e.castItem?.Version} available";
					Growl.Info($"Update {e.castItem?.Version} available.");
					break;
				case AppUpdateCheckStatus.NoUpdate:
					Log.Info("You are running the latest version.");
					break;
				case AppUpdateCheckStatus.CouldNotDetermine:
					Log.Warn("Could not check for updates.");
					break;
			}
		});
	}

	public void SaveSettings()
	{
		try
		{
			SettingsStore.Save(Settings);
		}
		catch (Exception ex)
		{
			Log.Warn($"Failed to save settings: {ex.Message}");
		}
	}

	public void WriteLogToDisk()
	{
		try
		{
			SettingsStore.WriteLog(Log.Snapshot());
		}
		catch { }
	}
}
