using LibGit2Sharp;

namespace UnrealBinaryBuilder.Core.Tools;

/// <summary>Lightweight read-only git info for displaying engine commit details.</summary>
public static class GitInfo
{
	public static GitSnapshot? Read(string repoPath)
	{
		if (string.IsNullOrWhiteSpace(repoPath) || !Repository.IsValid(repoPath))
		{
			return null;
		}

		try
		{
			using var repo = new Repository(repoPath);
			string sha = repo.Head.Tip.Sha;
			string shortSha = sha.Length > 7 ? sha[..7] : sha;
			string branch = repo.Head.FriendlyName;
			string? upstream = repo.Head.IsTracking ? repo.Head.TrackedBranch?.FriendlyName : null;
			return new GitSnapshot(sha, shortSha, branch, upstream);
		}
		catch
		{
			return null;
		}
	}
}

public sealed record GitSnapshot(string CommitSha, string ShortSha, string Branch, string? UpstreamBranch);
