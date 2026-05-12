using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using UnrealBinaryBuilder.Core.Logging;

namespace UnrealBinaryBuilder.Core.Zip;

public sealed class ZipBuilder
{
	private readonly IBuildLogger _logger;
	private readonly IZipProgress _progress;

	public ZipBuilder(IBuildLogger logger, IZipProgress? progress = null)
	{
		_logger = logger;
		_progress = progress ?? NullZipProgress.Instance;
	}

	public Task ZipEngineBuildAsync(string buildDirectory, string outputZipPath, EngineZipOptions options, CancellationToken cancellationToken = default)
		=> Task.Run(() => ZipEngineBuildCore(buildDirectory, outputZipPath, options, cancellationToken), cancellationToken);

	public Task ZipPluginAsync(string pluginOutputDirectory, string outputZipPath, PluginZipOptions options, CancellationToken cancellationToken = default)
		=> Task.Run(() => ZipPluginCore(pluginOutputDirectory, outputZipPath, options, cancellationToken), cancellationToken);

	private void ZipEngineBuildCore(string buildDirectory, string outputZipPath, EngineZipOptions options, CancellationToken cancellationToken)
	{
		_logger.Info($"Filtering files in {buildDirectory}...");
		var allFiles = Directory.EnumerateFiles(buildDirectory, "*.*", SearchOption.AllDirectories).ToList();

		var included = new List<string>();
		long totalBytes = 0;
		long includedBytes = 0;

		foreach (string file in allFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();
			long size = new FileInfo(file).Length;
			totalBytes += size;
			if (!ShouldSkip(file, options))
			{
				included.Add(file);
				includedBytes += size;
			}
		}

		_progress.OnFilteringComplete(allFiles.Count, included.Count, totalBytes, includedBytes);
		_logger.Info($"Including {included.Count} of {allFiles.Count} files ({BytesToString(includedBytes)} of {BytesToString(totalBytes)}).");

		WriteArchive(included, buildDirectory, outputZipPath, options.CompressionLevel, includedBytes, cancellationToken);
		_progress.OnFinished(outputZipPath);
		_logger.Info($"Engine zip saved to {outputZipPath}");
	}

	private void ZipPluginCore(string pluginOutputDirectory, string outputZipPath, PluginZipOptions options, CancellationToken cancellationToken)
	{
		var allFiles = Directory.EnumerateFiles(pluginOutputDirectory, "*.*", SearchOption.AllDirectories);
		var included = new List<string>();
		long includedBytes = 0;
		foreach (string file in allFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string normalised = Path.GetFullPath(file).ToLowerInvariant();
			if (options.ZipForMarketplace && (normalised.Contains(@"\binaries\") || normalised.Contains(@"\intermediate\")))
			{
				continue;
			}
			included.Add(file);
			includedBytes += new FileInfo(file).Length;
		}

		_progress.OnFilteringComplete(included.Count, included.Count, includedBytes, includedBytes);
		WriteArchive(included, pluginOutputDirectory, outputZipPath, options.CompressionLevel, includedBytes, cancellationToken);
		_progress.OnFinished(outputZipPath);
		_logger.Info($"Plugin zip saved to {outputZipPath}");
	}

	private void WriteArchive(IReadOnlyList<string> files, string baseDir, string outputZipPath, CompressionLevel level, long totalBytes, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath)!);
		if (File.Exists(outputZipPath))
		{
			File.Delete(outputZipPath);
		}

		using var fs = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write);
		using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

		long processedBytes = 0;
		for (int i = 0; i < files.Count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string file = files[i];
			string entryName = Path.GetRelativePath(baseDir, file).Replace('\\', '/');

			ZipArchiveEntry entry = archive.CreateEntry(entryName, level);
			using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (var output = entry.Open())
			{
				var buffer = new byte[81920];
				int read;
				while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
				{
					cancellationToken.ThrowIfCancellationRequested();
					output.Write(buffer, 0, read);
					processedBytes += read;
				}
			}

			_progress.OnEntry(new ZipProgressEvent(
				CurrentFile: Path.GetFileName(file),
				ProcessedFiles: i + 1,
				TotalFiles: files.Count,
				ProcessedBytes: processedBytes,
				TotalBytes: totalBytes));
		}
	}

	private static bool ShouldSkip(string file, EngineZipOptions o)
	{
		string path = Path.GetFullPath(file).ToLowerInvariant();
		string ext = Path.GetExtension(file).ToLowerInvariant();

		if (!o.IncludePdb && ext == ".pdb") return true;
		if (!o.IncludeDebug && ext == ".debug") return true;
		if (!o.IncludeDocumentation && !path.Contains(@"\source\") && path.Contains(@"\documentation\")) return true;
		if (!o.IncludeExtras && !path.Contains(@"\extras\redist\") && path.Contains(@"\extras\")) return true;
		if (!o.IncludeSource && (
			path.Contains(@"\source\developer\") ||
			path.Contains(@"\source\editor\") ||
			path.Contains(@"\source\programs\") ||
			path.Contains(@"\source\runtime\") ||
			path.Contains(@"\source\thirdparty\"))) return true;
		if (!o.IncludeFeaturePacks && path.Contains(@"\featurepacks\")) return true;
		if (!o.IncludeSamples && path.Contains(@"\samples\")) return true;
		if (!o.IncludeTemplates &&
			!path.Contains(@"\source\") &&
			!path.Contains(@"\content\editor") &&
			path.Contains(@"\templates\")) return true;
		return false;
	}

	public static string BytesToString(long byteCount)
	{
		string[] suf = { "B", "KB", "MB", "GB", "TB" };
		if (byteCount == 0) return "0B";
		long bytes = Math.Abs(byteCount);
		int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
		double num = Math.Round(bytes / Math.Pow(1024, place), 1);
		return (Math.Sign(byteCount) * num).ToString() + suf[place];
	}
}
