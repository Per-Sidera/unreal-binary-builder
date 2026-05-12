using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using NetSparkleUpdater;
using NetSparkleUpdater.AppCastHandlers;
using NetSparkleUpdater.AssemblyAccessors;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Events;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.WPF;

namespace UnrealBinaryBuilderUpdater;

public enum AppUpdateCheckStatus
{
	UpdateAvailable,
	NoUpdate,
	UserSkip,
	CouldNotDetermine
}

public class UBBUpdater
{
	// Set this when you publish your own fork. While empty, update checks short-circuit.
	public static string AppCastXml { get; set; } = string.Empty;

	// Empty key disables verification — set this to your Ed25519 public key
	// (base64) before publishing a real release.
	public static string Ed25519PublicKey { get; set; } = string.Empty;

	private static UpdateInfo? _updateInfo;
	private static SparkleUpdater? _sparkle;
	private string? _downloadPath;

	public event EventHandler<UpdateProgressFinishedEventArgs>? SilentUpdateFinishedEventHandler;
	public event EventHandler<UpdateProgressDownloadEventArgs>? UpdateProgressEventHandler;
	public event EventHandler<UpdateProgressDownloadErrorEventArgs>? UpdateProgressDownloadErrorEventHandler;
	public event EventHandler<UpdateProgressDownloadStartEventArgs>? UpdateDownloadStartedEventHandler;
	public event EventHandler<UpdateProgressDownloadFinishEventArgs>? UpdateDownloadFinishedEventHandler;
	public event EventHandler? CloseApplicationEventHandler;

	public UBBUpdater()
	{
		EnsureSparkle();
	}

	public static bool IsConfigured => !string.IsNullOrWhiteSpace(AppCastXml);

	private static void EnsureSparkle()
	{
		if (!IsConfigured)
		{
			return;
		}

		if (_sparkle == null)
		{
			var verifier = string.IsNullOrEmpty(Ed25519PublicKey)
				? new Ed25519Checker(SecurityMode.Unsafe, null)
				: new Ed25519Checker(SecurityMode.Strict, Ed25519PublicKey);

			_sparkle = new SparkleUpdater(AppCastXml, verifier)
			{
				ShowsUIOnMainThread = false,
				UseNotificationToast = true,
				LogWriter = new LogWriter()
			};
		}

		if (_sparkle.UIFactory == null)
		{
			string entry = Assembly.GetEntryAssembly()!.ManifestModule.FullyQualifiedName;
			var icon = System.Drawing.Icon.ExtractAssociatedIcon(entry);
			_sparkle.UIFactory = new UIFactory(IconUtilities.ToImageSource(icon!));
		}

		_sparkle.SecurityProtocolType = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
	}

	public void CheckForUpdates()
	{
		if (!IsConfigured) return;
		EnsureSparkle();
		_sparkle!.CheckForUpdatesAtUserRequest();
	}

	public async void CheckForUpdatesSilently()
	{
		if (!IsConfigured) return;
		EnsureSparkle();
		_sparkle!.UIFactory = null;
		_updateInfo = await _sparkle.CheckForUpdatesQuietly();
		if (_updateInfo == null)
		{
			return;
		}

		var args = new UpdateProgressFinishedEventArgs();
		switch (_updateInfo.Status)
		{
			case UpdateStatus.UpdateAvailable:
				args.appUpdateCheckStatus = AppUpdateCheckStatus.UpdateAvailable;
				args.castItem = _updateInfo.Updates.First();
				break;
			case UpdateStatus.UpdateNotAvailable:
				args.appUpdateCheckStatus = AppUpdateCheckStatus.NoUpdate;
				break;
			case UpdateStatus.UserSkipped:
				args.appUpdateCheckStatus = AppUpdateCheckStatus.UserSkip;
				break;
			case UpdateStatus.CouldNotDetermine:
				args.appUpdateCheckStatus = AppUpdateCheckStatus.CouldNotDetermine;
				break;
		}

		SilentUpdateFinishedEventHandler?.Invoke(this, args);
	}

	public async void DownloadUpdate()
	{
		if (_sparkle == null || _updateInfo == null)
		{
			return;
		}

		_sparkle.DownloadStarted -= OnDownloadStart;
		_sparkle.DownloadStarted += OnDownloadStart;
		_sparkle.DownloadFinished -= OnDownloadFinish;
		_sparkle.DownloadFinished += OnDownloadFinish;
		_sparkle.DownloadHadError -= OnDownloadError;
		_sparkle.DownloadHadError += OnDownloadError;
		_sparkle.DownloadMadeProgress += OnDownloadMadeProgress;

		await _sparkle.InitAndBeginDownload(_updateInfo.Updates.First());
	}

	public void InstallUpdate() => CloseApplicationEventHandler?.Invoke(this, EventArgs.Empty);

	private void OnDownloadStart(AppCastItem item, string path)
	{
		UpdateDownloadStartedEventHandler?.Invoke(this, new UpdateProgressDownloadStartEventArgs
		{
			UpdateSize = item.UpdateSize,
			Version = item.Version
		});
	}

	private void OnDownloadFinish(AppCastItem item, string path)
	{
		_downloadPath = path;
		string newFile = _downloadPath + ".zip";
		if (File.Exists(newFile))
		{
			File.Delete(newFile);
		}
		File.Move(_downloadPath, newFile);

		UpdateDownloadFinishedEventHandler?.Invoke(this, new UpdateProgressDownloadFinishEventArgs
		{
			UpdateFilePath = newFile,
			castItem = item
		});
	}

	private void OnDownloadMadeProgress(object? sender, AppCastItem appCastItem, ItemDownloadProgressEventArgs e)
	{
		UpdateProgressEventHandler?.Invoke(this, new UpdateProgressDownloadEventArgs
		{
			AppUpdateProgress = e.ProgressPercentage
		});
	}

	private void OnDownloadError(AppCastItem item, string path, Exception ex)
	{
		UpdateProgressDownloadErrorEventHandler?.Invoke(this, new UpdateProgressDownloadErrorEventArgs
		{
			ErrorException = ex
		});
	}
}

public class UpdateProgressFinishedEventArgs : EventArgs
{
	public AppUpdateCheckStatus appUpdateCheckStatus { get; set; }
	public AppCastItem? castItem { get; set; }
}

public class UpdateProgressDownloadEventArgs : EventArgs
{
	public int AppUpdateProgress { get; set; }
}

public class UpdateProgressDownloadErrorEventArgs : EventArgs
{
	public Exception? ErrorException { get; set; }
}

public class UpdateProgressDownloadStartEventArgs : EventArgs
{
	public long UpdateSize { get; set; }
	public string? Version { get; set; }
}

public class UpdateProgressDownloadFinishEventArgs : EventArgs
{
	public AppCastItem? castItem { get; set; }
	public string? UpdateFilePath { get; set; }
}
