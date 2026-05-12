using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Plugins;

namespace UnrealBinaryBuilder.ViewModels;

public sealed partial class PluginCardViewModel : ObservableObject
{
	public PluginInfo PluginInfo { get; }
	public string DestinationDirectory { get; }
	public EngineInfo Engine { get; }
	public IReadOnlyList<string>? TargetPlatforms { get; }
	public bool ZipAfterBuild { get; }
	public string ZipDirectory { get; }
	public bool ZipForMarketplace { get; }

	[ObservableProperty] private PluginCardState _state = PluginCardState.Pending;

	public string Title => PluginInfo.FriendlyName;
	public string Description => PluginInfo.Description;
	public string EngineVersionLabel => Engine.Version?.ToString() ?? Engine.EngineName;

	public PluginCardViewModel(
		PluginInfo info,
		string destinationDir,
		EngineInfo engine,
		IReadOnlyList<string>? targetPlatforms,
		bool zipAfterBuild,
		string zipDirectory,
		bool zipForMarketplace)
	{
		PluginInfo = info;
		DestinationDirectory = destinationDir;
		Engine = engine;
		TargetPlatforms = targetPlatforms;
		ZipAfterBuild = zipAfterBuild;
		ZipDirectory = zipDirectory;
		ZipForMarketplace = zipForMarketplace;
	}
}

public enum PluginCardState
{
	Pending,
	Building,
	Succeeded,
	Failed
}
