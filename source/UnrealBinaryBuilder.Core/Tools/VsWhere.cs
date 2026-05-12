using System.Diagnostics;
using System.IO;

namespace UnrealBinaryBuilder.Core.Tools;

/// <summary>
/// Locates Visual Studio components via the bundled vswhere.exe shipped with VS Installer.
/// </summary>
public static class VsWhere
{
	private static string? _cachedMsBuild;

	public static string? VsWhereExePath
	{
		get
		{
			string? programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			if (string.IsNullOrWhiteSpace(programFilesX86)) return null;
			string p = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
			return File.Exists(p) ? p : null;
		}
	}

	public static string? FindMsBuildExe()
	{
		if (_cachedMsBuild != null && File.Exists(_cachedMsBuild)) return _cachedMsBuild;

		string? exe = VsWhereExePath;
		if (exe == null) return null;

		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = exe,
				Arguments = "-latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true
			};
			using var p = Process.Start(psi);
			if (p == null) return null;
			string stdout = p.StandardOutput.ReadToEnd();
			p.WaitForExit();
			string? line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(l => l.Trim())
				.FirstOrDefault(l => !string.IsNullOrEmpty(l) && File.Exists(l));
			if (line != null) _cachedMsBuild = line;
			return line;
		}
		catch
		{
			return null;
		}
	}

	public static string? FindLatestVisualStudioInstall()
	{
		string? exe = VsWhereExePath;
		if (exe == null) return null;

		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = exe,
				Arguments = "-latest -prerelease -products * -property installationPath",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true
			};
			using var p = Process.Start(psi);
			if (p == null) return null;
			string stdout = p.StandardOutput.ReadToEnd().Trim();
			p.WaitForExit();
			return Directory.Exists(stdout) ? stdout : null;
		}
		catch
		{
			return null;
		}
	}
}
