using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Logging;
using UnrealBinaryBuilder.Core.Plugins;
using UnrealBinaryBuilder.Core.Zip;

namespace UnrealBinaryBuilder.ViewModels;

public sealed partial class PluginsTabViewModel : ObservableObject
{
	public LogViewModel Log { get; }
	public StatusViewModel Status { get; }

	public ObservableCollection<EngineInfo> InstalledEngines { get; } = new();
	public ObservableCollection<PluginCardViewModel> Queue { get; } = new();
	public ObservableCollection<string> AllowedPlatforms { get; } = new()
	{
		"Win64", "Win32", "Mac", "Linux", "LinuxArm64",
		"Android", "IOS", "TVOS", "HoloLens",
		"Switch", "PS4", "PS5", "XboxOne", "XSX"
	};

	[ObservableProperty] private EngineInfo? _selectedEngine;
	[ObservableProperty] private string _pluginPath = string.Empty;
	[ObservableProperty] private string _destinationPath = string.Empty;
	[ObservableProperty] private bool _overrideTargetPlatforms;
	[ObservableProperty] private bool _zipAfterBuild;
	[ObservableProperty] private string _zipPath = string.Empty;
	[ObservableProperty] private bool _zipForMarketplace = true;

	public ObservableCollection<PlatformChoice> PlatformChoices { get; } = new();

	private CancellationTokenSource? _cts;

	public PluginsTabViewModel(LogViewModel log, StatusViewModel status)
	{
		Log = log;
		Status = status;
		foreach (string p in AllowedPlatforms)
		{
			PlatformChoices.Add(new PlatformChoice(p, p == "Win64", p == "Win64"));
		}
		LoadInstalledEngines();
	}

	private void LoadInstalledEngines()
	{
		InstalledEngines.Clear();
		foreach (var info in EngineDetector.EnumerateInstalledEngines())
		{
			string runUat = EngineDetector.RunUatBat(info.EnginePath);
			if (File.Exists(runUat))
			{
				InstalledEngines.Add(info);
			}
		}
		if (InstalledEngines.Count > 0 && SelectedEngine is null)
		{
			SelectedEngine = InstalledEngines[0];
		}
	}

	[RelayCommand]
	private void BrowsePluginPath()
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Unreal Plugin (*.uplugin)|*.uplugin"
		};
		if (dialog.ShowDialog() == true)
		{
			PluginPath = dialog.FileName;
			ApplyManifestPlatforms();
		}
	}

	[RelayCommand]
	private void BrowseDestinationPath()
	{
		using var dialog = new System.Windows.Forms.FolderBrowserDialog
		{
			Description = "Select destination directory",
			UseDescriptionForTitle = true
		};
		if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			DestinationPath = dialog.SelectedPath;
		}
	}

	[RelayCommand]
	private void BrowseZipPath()
	{
		using var dialog = new System.Windows.Forms.FolderBrowserDialog
		{
			Description = "Select directory to save plugin zip",
			UseDescriptionForTitle = true
		};
		if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			ZipPath = dialog.SelectedPath;
		}
	}

	private void ApplyManifestPlatforms()
	{
		var info = PluginManifestReader.Read(PluginPath);
		if (info?.AllowedPlatforms is null) return;

		foreach (var pc in PlatformChoices)
		{
			if (pc.Name == "Win64") continue;
			pc.IsSelected = info.AllowedPlatforms.Contains(pc.Name);
		}
	}

	[RelayCommand]
	private void EnqueuePlugin()
	{
		if (!File.Exists(PluginPath))
		{
			Growl.Warning("Choose a .uplugin file first.");
			return;
		}
		if (!Directory.Exists(DestinationPath))
		{
			Growl.Warning("Destination directory must exist.");
			return;
		}
		if (SelectedEngine is null)
		{
			Growl.Warning("Pick an installed engine.");
			return;
		}

		var info = PluginManifestReader.Read(PluginPath);
		if (info is null)
		{
			Growl.Error("Could not read .uplugin file.");
			return;
		}

		IReadOnlyList<string>? targets = null;
		if (OverrideTargetPlatforms)
		{
			targets = PlatformChoices.Where(p => p.IsSelected).Select(p => p.Name).ToList();
		}

		var card = new PluginCardViewModel(info, DestinationPath, SelectedEngine, targets,
			ZipAfterBuild, ZipPath, ZipForMarketplace);
		Queue.Add(card);

		// Reset inputs
		PluginPath = string.Empty;
		DestinationPath = string.Empty;
		ZipPath = string.Empty;
		foreach (var pc in PlatformChoices)
		{
			pc.IsSelected = pc.Name == "Win64";
		}
	}

	[RelayCommand]
	private void RemoveCard(PluginCardViewModel card)
	{
		Queue.Remove(card);
	}

	[RelayCommand]
	private async Task BuildQueueAsync()
	{
		if (Queue.Count == 0)
		{
			Growl.Info("Queue is empty.");
			return;
		}

		_cts = new CancellationTokenSource();
		Status.Start($"Building {Queue.Count} plugin(s)...");

		foreach (var card in Queue.ToList())
		{
			if (card.State != PluginCardState.Pending) continue;
			card.State = PluginCardState.Building;

			Log.Info($"=========== Plugin: {card.Title} (Engine {card.EngineVersionLabel}) ===========");
			var builder = new PluginBuilder(Log, Status);
			var request = new PluginBuildRequest(
				PluginFilePath: card.PluginInfo.PluginFilePath,
				DestinationDirectory: card.DestinationDirectory,
				TargetEngine: card.Engine,
				TargetPlatforms: card.TargetPlatforms);

			PluginBuildResult result;
			try
			{
				result = await builder.BuildAsync(request, _cts.Token);
			}
			catch (OperationCanceledException)
			{
				Log.Warn("Plugin queue canceled.");
				Status.Stop("Canceled.");
				return;
			}
			catch (Exception ex)
			{
				Log.Error($"Plugin build threw: {ex.Message}");
				card.State = PluginCardState.Failed;
				continue;
			}

			card.State = result.Success ? PluginCardState.Succeeded : PluginCardState.Failed;
			Log.Info($"Plugin {card.Title}: {(result.Success ? "succeeded" : "failed")} (errors {result.Errors}, warnings {result.Warnings}, took {result.Elapsed:hh\\:mm\\:ss})");

			if (result.Success && card.ZipAfterBuild && !string.IsNullOrEmpty(card.ZipDirectory))
			{
				try
				{
					string zipFile = Path.Combine(card.ZipDirectory,
						$"{Path.GetFileNameWithoutExtension(card.PluginInfo.PluginFilePath)}_{card.EngineVersionLabel}.zip");
					var zipper = new ZipBuilder(Log);
					await zipper.ZipPluginAsync(card.DestinationDirectory, zipFile,
						new PluginZipOptions(card.ZipForMarketplace));
				}
				catch (Exception ex)
				{
					Log.Error($"Zipping plugin failed: {ex.Message}");
				}
			}
		}

		Status.Stop("Plugin queue complete.");
	}
}

public sealed partial class PlatformChoice : ObservableObject
{
	public string Name { get; }
	public bool IsLocked { get; }
	[ObservableProperty] private bool _isSelected;

	public PlatformChoice(string name, bool initiallySelected, bool isLocked)
	{
		Name = name;
		IsLocked = isLocked;
		_isSelected = initiallySelected;
	}
}
