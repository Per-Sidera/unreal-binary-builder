using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using UnrealBinaryBuilder.Core.Build;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Logging;
using UnrealBinaryBuilder.Core.Settings;
using UnrealBinaryBuilder.Core.Tools;
using UnrealBinaryBuilder.Core.Zip;

namespace UnrealBinaryBuilder.ViewModels;

public sealed partial class EngineTabViewModel : ObservableObject
{
	public BuilderSettings Settings { get; }
	public LogViewModel Log { get; }
	public StatusViewModel Status { get; }
	public ZipProgressViewModel ZipProgress { get; }

	[ObservableProperty] private EngineVersion? _detectedVersion;
	[ObservableProperty] private GitSnapshot? _gitInfo;
	[ObservableProperty] private bool _canBuild;

	private CancellationTokenSource? _cts;

	public EngineTabViewModel(BuilderSettings settings, LogViewModel log, StatusViewModel status, ZipProgressViewModel zip)
	{
		Settings = settings;
		Log = log;
		Status = status;
		ZipProgress = zip;
		RefreshEngineRootDependentState();
	}

	public bool IsBuilding => Status.IsBusy;

	[RelayCommand]
	private void BrowseEngineRoot()
	{
		using var dialog = new System.Windows.Forms.FolderBrowserDialog
		{
			Description = "Select the Unreal Engine root folder (where Setup.bat lives)",
			UseDescriptionForTitle = true
		};
		if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			Settings.EngineRootPath = dialog.SelectedPath;
			OnPropertyChanged(nameof(Settings));
			RefreshEngineRootDependentState();
		}
	}

	[RelayCommand]
	private void BrowseZipPath()
	{
		var dialog = new Microsoft.Win32.SaveFileDialog
		{
			DefaultExt = ".zip",
			Filter = "Zip File (.zip)|*.zip",
			FileName = GitInfo?.ShortSha ?? "UnrealEngine.zip"
		};
		if (dialog.ShowDialog() == true)
		{
			Settings.ZipEnginePath = dialog.FileName;
			OnPropertyChanged(nameof(Settings));
		}
	}

	[RelayCommand]
	private void BrowseGitCachePath()
	{
		using var dialog = new System.Windows.Forms.FolderBrowserDialog
		{
			Description = "Select Git dependency cache folder",
			UseDescriptionForTitle = true
		};
		if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			Settings.GitDependencyCachePath = dialog.SelectedPath;
			OnPropertyChanged(nameof(Settings));
		}
	}

	[RelayCommand]
	private void BrowseCustomBuildXml()
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "XML file (*.xml)|*.xml"
		};
		if (dialog.ShowDialog() == true)
		{
			Settings.CustomBuildXmlFile = dialog.FileName;
			OnPropertyChanged(nameof(Settings));
		}
	}

	[RelayCommand]
	private void ResetCustomBuildXml()
	{
		Settings.CustomBuildXmlFile = string.Empty;
		OnPropertyChanged(nameof(Settings));
	}

	/// <summary>
	/// Refreshes every binding rooted at <see cref="Settings"/>. Call after the
	/// settings instance has been mutated wholesale (e.g. an imported preset).
	/// </summary>
	public void OnSettingsReloaded()
	{
		OnPropertyChanged(nameof(Settings));
		RefreshEngineRootDependentState();
	}

	[RelayCommand]
	private void RefreshEngineRootDependentState()
	{
		string root = Settings.EngineRootPath;
		if (EngineDetector.IsEngineRoot(root))
		{
			DetectedVersion = EngineDetector.ReadEngineVersion(root);
			GitInfo = Core.Tools.GitInfo.Read(root);
			CanBuild = true;
		}
		else
		{
			DetectedVersion = null;
			GitInfo = null;
			CanBuild = false;
		}
	}

	[RelayCommand]
	private async Task BuildAsync()
	{
		if (!CanBuild)
		{
			Growl.Warning("Set a valid Unreal Engine root before building.");
			return;
		}

		if (Settings.ShowEngineBuildConfirmation)
		{
			var result = HandyControl.Controls.MessageBox.Show(
				"Building a binary version of Unreal Engine takes a long time. Continue?",
				"Confirm build",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes) return;
		}

		Log.Clear();
		Status.Start("Building...");
		_cts = new CancellationTokenSource();

		var pipeline = new BuildPipeline(Log, Status);
		BuildOutcome outcome;
		try
		{
			outcome = await pipeline.RunAsync(Settings, _cts.Token);
		}
		catch (OperationCanceledException)
		{
			Log.Warn("Build canceled by user.");
			Status.Stop("Canceled.");
			return;
		}
		catch (Exception ex)
		{
			Log.Error($"Build threw an exception: {ex}");
			Status.Stop("Failed.");
			return;
		}

		Log.Info($"=========== BUILD {(outcome.Success ? "SUCCEEDED" : "FAILED")} ===========");
		Log.Info($"Last step: {outcome.LastStep}. Errors: {outcome.Errors}. Warnings: {outcome.Warnings}. Took {outcome.Elapsed:hh\\:mm\\:ss}.");

		Status.Stop(outcome.Success
			? $"Build complete. {outcome.Errors} errors, {outcome.Warnings} warnings, {outcome.Elapsed:hh\\:mm\\:ss}"
			: $"Build failed. {outcome.Errors} errors, {outcome.Warnings} warnings.");

		if (outcome.Success && Settings.ZipEngineBuild && !string.IsNullOrEmpty(Settings.ZipEnginePath))
		{
			await ZipEngineBuildAsync();
		}

		if (outcome.Success && Settings.ShutdownPc)
		{
			TryShutdown();
		}
	}

	[RelayCommand]
	private void Cancel()
	{
		_cts?.Cancel();
	}

	private async Task ZipEngineBuildAsync()
	{
		string? buildDir = BuildPipeline.ResolveBuildDir(Settings.EngineRootPath);
		if (buildDir is null)
		{
			Log.Error($"Could not find installed engine output under {Settings.EngineRootPath}\\LocalBuilds.");
			return;
		}

		Log.Info($"Zipping {buildDir} → {Settings.ZipEnginePath}");
		var zipper = new ZipBuilder(Log, ZipProgress);
		try
		{
			await zipper.ZipEngineBuildAsync(buildDir, Settings.ZipEnginePath, EngineZipOptions.FromSettings(Settings));
		}
		catch (Exception ex)
		{
			Log.Error($"Zipping failed: {ex.Message}");
		}
	}

	private void TryShutdown()
	{
		System.Diagnostics.Process.Start("shutdown", "/s /t 30");
		Application.Current.Shutdown();
	}
}
