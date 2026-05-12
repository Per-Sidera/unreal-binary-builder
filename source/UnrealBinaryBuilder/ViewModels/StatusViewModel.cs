using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using UnrealBinaryBuilder.Core.Tools;

namespace UnrealBinaryBuilder.ViewModels;

public sealed partial class StatusViewModel : ObservableObject, IProcessProgress
{
	private readonly Stopwatch _watch = new();
	private readonly DispatcherTimer _timer;

	[ObservableProperty] private string _status = "Idle.";
	[ObservableProperty] private string _step = "Step: -";
	[ObservableProperty] private int _filesCompiled;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string _elapsedDisplay = "00:00:00";

	public StatusViewModel()
	{
		_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_timer.Tick += (_, _) => ElapsedDisplay = _watch.Elapsed.ToString(@"hh\:mm\:ss");
	}

	public void Start(string status)
	{
		Status = status;
		IsBusy = true;
		FilesCompiled = 0;
		_watch.Restart();
		_timer.Start();
	}

	public void Stop(string status)
	{
		_watch.Stop();
		_timer.Stop();
		Status = status;
		IsBusy = false;
	}

	public TimeSpan Elapsed => _watch.Elapsed;

	void IProcessProgress.OnStep(int current, int total)
	{
		Application.Current.Dispatcher.BeginInvoke(() => Step = $"Step: {current}/{total}");
	}

	void IProcessProgress.OnFileCompiled(int totalSoFar)
	{
		Application.Current.Dispatcher.BeginInvoke(() => FilesCompiled = totalSoFar);
	}
}
