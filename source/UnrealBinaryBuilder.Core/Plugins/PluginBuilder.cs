using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnrealBinaryBuilder.Core.Engine;
using UnrealBinaryBuilder.Core.Logging;
using UnrealBinaryBuilder.Core.Tools;

namespace UnrealBinaryBuilder.Core.Plugins;

public sealed record PluginBuildRequest(
	string PluginFilePath,
	string DestinationDirectory,
	EngineInfo TargetEngine,
	IReadOnlyList<string>? TargetPlatforms = null);

public sealed record PluginBuildResult(bool Success, int ExitCode, int Errors, int Warnings, TimeSpan Elapsed);

public sealed class PluginBuilder
{
	private readonly IBuildLogger _logger;
	private readonly IProcessProgress? _progress;

	public PluginBuilder(IBuildLogger logger, IProcessProgress? progress = null)
	{
		_logger = logger;
		_progress = progress;
	}

	public async Task<PluginBuildResult> BuildAsync(PluginBuildRequest request, CancellationToken cancellationToken = default)
	{
		string runUat = EngineDetector.RunUatBat(request.TargetEngine.EnginePath);
		if (!File.Exists(runUat))
		{
			_logger.Error($"RunUAT.bat not found in {request.TargetEngine.EnginePath}.");
			return new PluginBuildResult(false, -1, 1, 0, TimeSpan.Zero);
		}

		var args = new StringBuilder();
		args.Append("BuildPlugin");
		args.Append($" -Plugin=\"{request.PluginFilePath}\"");
		args.Append($" -Package=\"{request.DestinationDirectory}\"");
		args.Append(" -Rocket");

		if (request.TargetPlatforms is { Count: > 0 })
		{
			args.Append(" -TargetPlatforms=").Append(string.Join('+', request.TargetPlatforms));
		}

		var runner = new ProcessRunner(_logger, _progress);
		var result = await runner.RunAsync(new ProcessOptions(
			FileName: runUat,
			Arguments: args.ToString(),
			WorkingDirectory: request.TargetEngine.EnginePath), cancellationToken);

		return new PluginBuildResult(result.ExitCode == 0, result.ExitCode, result.Errors, result.Warnings, result.Elapsed);
	}
}
