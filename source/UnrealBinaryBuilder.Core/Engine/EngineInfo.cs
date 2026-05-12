namespace UnrealBinaryBuilder.Core.Engine;

public sealed record EngineVersion(int Major, int Minor, int Patch)
{
	public bool IsUnreal5 => Major >= 5;
	public bool IsUnreal4 => Major == 4;
	public double AsDouble => double.Parse($"{Major}.{Minor:D2}", System.Globalization.CultureInfo.InvariantCulture);

	public override string ToString() => $"{Major}.{Minor}.{Patch}";

	public static EngineVersion? Parse(string? input)
	{
		if (string.IsNullOrWhiteSpace(input)) return null;
		var parts = input.Split('.');
		if (parts.Length < 2) return null;
		if (!int.TryParse(parts[0], out int maj)) return null;
		if (!int.TryParse(parts[1], out int min)) return null;
		int patch = 0;
		if (parts.Length > 2) int.TryParse(parts[2], out patch);
		return new EngineVersion(maj, min, patch);
	}
}

public sealed record EngineInfo(
	string EngineName,
	string EnginePath,
	EngineVersion? Version,
	bool IsCustom,
	string EngineAssociation
);
