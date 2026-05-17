using System.IO;
using System.Text;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Logging;

namespace UnrealBinaryBuilder.Core.Build;

/// <summary>
/// Writes a Register_Engine.bat next to a binary engine build. The .bat, when
/// run on the recipient's machine, registers the engine under
/// HKCU\Software\Epic Games\Unreal Engine\Builds so it shows up in the
/// .uproject "Switch Unreal Engine Version" picker, and invokes
/// UnrealVersionSelector.exe /register to wire up file associations and the
/// shell context menu. No admin rights required.
/// </summary>
public static class EngineRegistrationScript
{
	public const string FileName = "Register_Engine.bat";

	public static string? Write(string buildDirectory, string buildName, IBuildLogger logger)
	{
		if (!Directory.Exists(buildDirectory))
		{
			logger.Warn($"Cannot write {FileName}: build directory '{buildDirectory}' does not exist.");
			return null;
		}

		string resolvedName = ResolveName(buildName, buildDirectory);
		string path = Path.Combine(buildDirectory, FileName);
		// cmd.exe does not strip a UTF-8 BOM, so a BOM prepended to '@echo off'
		// becomes garbage on the first line ('∩╗┐@echo' is not recognized...).
		// Write ASCII-only (the script body is ASCII) via UTF-8-without-BOM.
		File.WriteAllText(path, BuildScript(resolvedName), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		logger.Info($"Wrote {FileName} (build name: {resolvedName}) to {path}");
		return path;
	}

	public static string ResolveName(string requested, string buildDirectory)
	{
		string trimmed = (requested ?? string.Empty).Trim();
		if (!string.IsNullOrEmpty(trimmed))
		{
			return Sanitize(trimmed);
		}

		EngineVersion? version = EngineDetector.ReadEngineVersion(buildDirectory);
		if (version is not null)
		{
			return $"UnrealEngine_{version.Major}_{version.Minor}_{version.Patch}";
		}
		return "UnrealEngine_Custom";
	}

	private static string Sanitize(string name)
	{
		var chars = name.ToCharArray();
		for (int i = 0; i < chars.Length; i++)
		{
			char c = chars[i];
			bool ok = char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';
			if (!ok) chars[i] = '_';
		}
		return new string(chars);
	}

	private static string BuildScript(string buildName) =>
$@"@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM Register_Engine.bat
REM
REM Registers this binary engine build with the local Unreal toolchain so it
REM appears in the .uproject ""Switch Unreal Engine Version"" picker. Runs
REM under the current user only — no admin elevation required.
REM
REM To use a different name, edit the BUILD_NAME line below before running.
REM In your .uproject, set:    ""EngineAssociation"": ""{buildName}""
REM ============================================================================

set ""BUILD_NAME={buildName}""

set ""ENGINE_DIR=%~dp0""
if ""%ENGINE_DIR:~-1%""==""\"" set ""ENGINE_DIR=%ENGINE_DIR:~0,-1%""

echo.
echo Registering Unreal Engine build
echo   Name : %BUILD_NAME%
echo   Path : %ENGINE_DIR%
echo.

reg add ""HKCU\Software\Epic Games\Unreal Engine\Builds"" /v ""%BUILD_NAME%"" /t REG_SZ /d ""%ENGINE_DIR%"" /f >nul
if errorlevel 1 (
    echo [ERROR] Failed to write registry value.
    echo Make sure no AV / policy is blocking writes to HKCU.
    pause
    exit /b 1
)

set ""UVS=%ENGINE_DIR%\Engine\Binaries\Win64\UnrealVersionSelector.exe""
if exist ""%UVS%"" (
    echo Associating .uproject files with this engine...
    ""%UVS%"" /register
    if errorlevel 1 echo [WARN] UnrealVersionSelector returned a non-zero exit code.
) else (
    echo [Note] UnrealVersionSelector.exe not found at:
    echo        %UVS%
    echo Engine is still registered, but .uproject double-click association
    echo and the ""Generate Visual Studio project files"" right-click entry will
    echo not be installed.
)

echo.
echo Done. To use this engine in a project, open the .uproject file or set:
echo     ""EngineAssociation"": ""%BUILD_NAME%""
echo in the .uproject manually.
echo.
pause
endlocal
";
}
