using System.IO.Compression;
using UnrealBinaryBuilder.Core.Settings;

namespace UnrealBinaryBuilder.Core.Zip;

public sealed record EngineZipOptions(
	bool IncludePdb,
	bool IncludeDebug,
	bool IncludeDocumentation,
	bool IncludeExtras,
	bool IncludeSource,
	bool IncludeFeaturePacks,
	bool IncludeSamples,
	bool IncludeTemplates,
	CompressionLevel CompressionLevel)
{
	public static EngineZipOptions FromSettings(BuilderSettings s) => new(
		IncludePdb: s.ZipIncludePdb,
		IncludeDebug: s.ZipIncludeDebug,
		IncludeDocumentation: s.ZipIncludeDocumentation,
		IncludeExtras: s.ZipIncludeExtras,
		IncludeSource: s.ZipIncludeSource,
		IncludeFeaturePacks: s.ZipIncludeFeaturePacks,
		IncludeSamples: s.ZipIncludeSamples,
		IncludeTemplates: s.ZipIncludeTemplates,
		CompressionLevel: s.ZipFastCompression ? CompressionLevel.Fastest : CompressionLevel.SmallestSize);
}

public sealed record PluginZipOptions(bool ZipForMarketplace, CompressionLevel CompressionLevel = CompressionLevel.Fastest);

public sealed record ZipProgressEvent(
	string CurrentFile,
	long ProcessedFiles,
	long TotalFiles,
	long ProcessedBytes,
	long TotalBytes);

public interface IZipProgress
{
	void OnFilteringComplete(long totalFiles, long includedFiles, long totalBytes, long includedBytes);
	void OnEntry(ZipProgressEvent ev);
	void OnFinished(string outputPath);
}

public sealed class NullZipProgress : IZipProgress
{
	public static readonly NullZipProgress Instance = new();
	public void OnFilteringComplete(long totalFiles, long includedFiles, long totalBytes, long includedBytes) { }
	public void OnEntry(ZipProgressEvent ev) { }
	public void OnFinished(string outputPath) { }
}
