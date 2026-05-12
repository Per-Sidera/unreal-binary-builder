using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using UnrealBinaryBuilder.Core.Logging;

namespace UnrealBinaryBuilder.ViewModels;

public sealed class LogEntry
{
	public DateTime Timestamp { get; init; } = DateTime.Now;
	public LogLevel Level { get; init; }
	public string Message { get; init; } = string.Empty;
	public Brush Color => Level switch
	{
		LogLevel.Error => Brushes.Tomato,
		LogLevel.Warning => Brushes.Goldenrod,
		LogLevel.Debug => Brushes.LightSkyBlue,
		LogLevel.Trace => Brushes.Gray,
		_ => Brushes.WhiteSmoke
	};
}

public sealed partial class LogViewModel : ObservableObject, IBuildLogger
{
	public ObservableCollection<LogEntry> Entries { get; } = new();

	[ObservableProperty]
	private int _errorCount;

	[ObservableProperty]
	private int _warningCount;

	public void Log(LogLevel level, string message)
	{
		if (string.IsNullOrEmpty(message)) return;

		var entry = new LogEntry { Level = level, Message = message };
		Application.Current.Dispatcher.BeginInvoke(() =>
		{
			Entries.Add(entry);
			if (level == LogLevel.Error) ErrorCount++;
			else if (level == LogLevel.Warning) WarningCount++;

			// Cap so the panel stays responsive on long builds.
			while (Entries.Count > 5000)
			{
				Entries.RemoveAt(0);
			}
		});
	}

	public void Clear()
	{
		Application.Current.Dispatcher.Invoke(() =>
		{
			Entries.Clear();
			ErrorCount = 0;
			WarningCount = 0;
		});
	}

	public string Snapshot()
	{
		var sb = new System.Text.StringBuilder();
		foreach (var e in Entries)
		{
			sb.AppendLine($"[{e.Timestamp:HH:mm:ss}] {e.Level,-7} {e.Message}");
		}
		return sb.ToString();
	}
}
