using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace UnrealBinaryBuilder.Theming;

/// <summary>
/// HandyControl 3.5.1 ships only Chinese strings (no English satellite
/// assembly), so its Lang.ResourceManager resolves "Cancel"/"Confirm"/etc.
/// to '取消'/'确定' regardless of CurrentCulture or ConfigHelper.Lang.
///
/// This wrapper intercepts a small set of keys (only what HC's ColorPicker
/// surfaces — confirm/cancel buttons) and returns English. Anything else
/// falls through to HC's original ResourceManager.
///
/// Install it once at app startup via <see cref="Install"/>; it patches the
/// private static <c>resourceMan</c> field on
/// <c>HandyControl.Properties.Langs.Lang</c> via reflection.
/// </summary>
internal sealed class HandyControlEnglishStrings : ResourceManager
{
	private static readonly Dictionary<string, string> English = new(System.StringComparer.Ordinal)
	{
		["Cancel"] = "Cancel",
		["Confirm"] = "OK",
	};

	private readonly ResourceManager _inner;

	private HandyControlEnglishStrings(ResourceManager inner, Assembly hcAssembly)
		: base("HandyControl.Properties.Langs.Lang", hcAssembly)
	{
		_inner = inner;
	}

	public override string? GetString(string name) =>
		English.TryGetValue(name, out var v) ? v : _inner.GetString(name);

	public override string? GetString(string name, CultureInfo? culture) =>
		English.TryGetValue(name, out var v) ? v : _inner.GetString(name, culture);

	/// <summary>
	/// Replaces HandyControl's <c>Lang.resourceMan</c> with a wrapper that
	/// returns English for known keys. Idempotent and silent on failure —
	/// HC's internal layout could shift between versions.
	/// </summary>
	public static void Install()
	{
		try
		{
			var langType = typeof(HandyControl.Data.HandyControlConfig).Assembly
				.GetType("HandyControl.Properties.Langs.Lang");
			if (langType is null) return;

			// Force the lazy-loaded ResourceManager property to populate the
			// private static field with HC's real ResourceManager.
			var prop = langType.GetProperty("ResourceManager", BindingFlags.Public | BindingFlags.Static);
			var original = prop?.GetValue(null) as ResourceManager;
			if (original is null or HandyControlEnglishStrings) return;

			var field = langType.GetField("resourceMan", BindingFlags.NonPublic | BindingFlags.Static);
			field?.SetValue(null, new HandyControlEnglishStrings(original, langType.Assembly));
		}
		catch
		{
			// HC layout changed — leave the original in place. The worst case
			// is a couple of Chinese button labels, not a crash.
		}
	}
}
