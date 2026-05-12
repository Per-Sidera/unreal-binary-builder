using System.IO;
using System.Text.Json;

namespace UnrealBinaryBuilder.Core.Settings;

/// <summary>Reads and writes <see cref="BuilderSettings"/> from disk.</summary>
public sealed class SettingsStore
{
	public static string DefaultRoot { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
		"UnrealBinaryBuilder");

	public static JsonSerializerOptions JsonOptions { get; } = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	private static JsonSerializerOptions Options => JsonOptions;

	public string Root { get; }
	public string SettingsPath => Path.Combine(Root, "Saved", "Settings.json");
	public string LogDir => Path.Combine(Root, "Logs");
	public string LogPath => Path.Combine(LogDir, "UnrealBinaryBuilder.log");
	public string ErrorLogPath => Path.Combine(LogDir, "BuildErrors.log");
	public string GitCacheDefaultPath => Path.Combine(Root, "GitCache");
	public string UpdatesDir => Path.Combine(Root, "Updates");

	public SettingsStore(string? root = null)
	{
		Root = root ?? DefaultRoot;
	}

	public BuilderSettings Load()
	{
		EnsureDirectories();
		if (!File.Exists(SettingsPath))
		{
			var fresh = CreateDefault();
			Save(fresh);
			return fresh;
		}

		try
		{
			string json = File.ReadAllText(SettingsPath);
			return JsonSerializer.Deserialize<BuilderSettings>(json, Options) ?? CreateDefault();
		}
		catch
		{
			// Corrupt/incompatible file — keep the bad copy aside and start fresh.
			try { File.Move(SettingsPath, SettingsPath + ".broken", overwrite: true); } catch { }
			var fresh = CreateDefault();
			Save(fresh);
			return fresh;
		}
	}

	public void Save(BuilderSettings settings)
	{
		EnsureDirectories();
		string json = JsonSerializer.Serialize(settings, Options);
		File.WriteAllText(SettingsPath, json);
	}

	public void WriteLog(string content)
	{
		EnsureDirectories();
		File.WriteAllText(LogPath, content);
	}

	public void WriteErrors(string? content)
	{
		EnsureDirectories();
		try { if (File.Exists(ErrorLogPath)) File.Delete(ErrorLogPath); } catch { }
		if (!string.IsNullOrWhiteSpace(content))
		{
			File.WriteAllText(ErrorLogPath, content);
		}
	}

	private BuilderSettings CreateDefault()
	{
		var s = new BuilderSettings();
		if (string.IsNullOrEmpty(s.GitDependencyCachePath))
		{
			s.GitDependencyCachePath = GitCacheDefaultPath;
		}
		return s;
	}

	private void EnsureDirectories()
	{
		Directory.CreateDirectory(Path.Combine(Root, "Saved"));
		Directory.CreateDirectory(LogDir);
		Directory.CreateDirectory(GitCacheDefaultPath);
	}
}
