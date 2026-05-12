using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnrealBinaryBuilder.Core.Logging;

namespace UnrealBinaryBuilder.Core.Tools;

public sealed record ProcessOptions(
	string FileName,
	string Arguments,
	string? WorkingDirectory = null);

public sealed record ProcessResult(int ExitCode, int Errors, int Warnings, TimeSpan Elapsed);

public sealed class ProcessRunner
{
	private static readonly Regex StepPattern = new(@"\*{6} \[(\d+)\/(\d+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex WarningPattern = new(@"warning|\*\*\* Unable to determine", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex ErrorPattern = new(@"Error_Unknown|ERROR|exited with code 1", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex CompiledFilePattern = new(@"\w.+\.(cpp|cc|c|h|ispc)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	public IBuildLogger Logger { get; }
	public IProcessProgress? Progress { get; }

	public ProcessRunner(IBuildLogger logger, IProcessProgress? progress = null)
	{
		Logger = logger;
		Progress = progress;
	}

	public async Task<ProcessResult> RunAsync(ProcessOptions options, CancellationToken cancellationToken = default)
	{
		var psi = new ProcessStartInfo
		{
			FileName = options.FileName,
			Arguments = options.Arguments,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		if (!string.IsNullOrEmpty(options.WorkingDirectory))
		{
			psi.WorkingDirectory = options.WorkingDirectory;
		}

		using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

		int errors = 0;
		int warnings = 0;
		int compiled = 0;
		var sw = Stopwatch.StartNew();

		var outputDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var errorDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		process.OutputDataReceived += (_, e) =>
		{
			if (e.Data is null) { outputDone.TrySetResult(true); return; }
			Classify(e.Data, ref warnings, ref errors, ref compiled, isErrorStream: false);
		};
		process.ErrorDataReceived += (_, e) =>
		{
			if (e.Data is null) { errorDone.TrySetResult(true); return; }
			errors++;
			Logger.Error(e.Data);
		};

		Logger.Info($"========================== RUNNING: {System.IO.Path.GetFileName(options.FileName)} ==========================");
		if (!string.IsNullOrEmpty(options.Arguments))
		{
			Logger.Debug($"Args: {options.Arguments}");
		}

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		using (cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { } }))
		{
			await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
		}

		await Task.WhenAll(outputDone.Task, errorDone.Task).ConfigureAwait(false);
		sw.Stop();

		Logger.Info($"Exit code: {process.ExitCode}. Errors: {errors}. Warnings: {warnings}. Compiled approx {compiled} files. Took {sw.Elapsed:hh\\:mm\\:ss}.");
		return new ProcessResult(process.ExitCode, errors, warnings, sw.Elapsed);
	}

	private void Classify(string line, ref int warnings, ref int errors, ref int compiled, bool isErrorStream)
	{
		var match = StepPattern.Match(line);
		if (match.Success)
		{
			Progress?.OnStep(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
		}
		if (CompiledFilePattern.IsMatch(line))
		{
			compiled++;
			Progress?.OnFileCompiled(compiled);
		}

		if (WarningPattern.IsMatch(line))
		{
			warnings++;
			Logger.Warn(line);
		}
		else if (ErrorPattern.IsMatch(line)
			&& !line.Contains("ShadowError")
			&& !line.Contains("error_details.")
			&& !line.Contains("error_code."))
		{
			errors++;
			Logger.Error(line);
		}
		else
		{
			Logger.Info(line);
		}
	}
}

public interface IProcessProgress
{
	void OnStep(int current, int total);
	void OnFileCompiled(int totalSoFar);
}

public sealed class NullProcessProgress : IProcessProgress
{
	public static readonly NullProcessProgress Instance = new();
	public void OnStep(int current, int total) { }
	public void OnFileCompiled(int totalSoFar) { }
}
