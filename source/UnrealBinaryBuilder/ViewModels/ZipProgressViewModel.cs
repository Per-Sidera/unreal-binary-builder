using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using UnrealBinaryBuilder.Core.Zip;

namespace UnrealBinaryBuilder.ViewModels;

public sealed partial class ZipProgressViewModel : ObservableObject, IZipProgress
{
	[ObservableProperty] private bool _isActive;
	[ObservableProperty] private string _state = "Idle";
	[ObservableProperty] private string _currentFile = string.Empty;
	[ObservableProperty] private string _stats = string.Empty;
	[ObservableProperty] private double _overallProgress;
	[ObservableProperty] private double _overallProgressMax = 100;
	[ObservableProperty] private string _outputPath = string.Empty;

	public void OnFilteringComplete(long totalFiles, long includedFiles, long totalBytes, long includedBytes)
	{
		Application.Current.Dispatcher.BeginInvoke(() =>
		{
			IsActive = true;
			OverallProgressMax = includedFiles;
			OverallProgress = 0;
			Stats = $"Total {totalFiles} files ({ZipBuilder.BytesToString(totalBytes)}). Including {includedFiles} ({ZipBuilder.BytesToString(includedBytes)}).";
			State = "Compressing...";
		});
	}

	public void OnEntry(ZipProgressEvent ev)
	{
		Application.Current.Dispatcher.BeginInvoke(() =>
		{
			OverallProgress = ev.ProcessedFiles;
			CurrentFile = $"{ev.CurrentFile} ({ev.ProcessedFiles}/{ev.TotalFiles})";
		});
	}

	public void OnFinished(string outputPath)
	{
		Application.Current.Dispatcher.BeginInvoke(() =>
		{
			IsActive = false;
			State = $"Saved to {outputPath}";
			OutputPath = outputPath;
		});
	}
}
