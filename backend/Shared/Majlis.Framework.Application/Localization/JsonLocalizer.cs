using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Majlis.Framework.Application.Localization;

/// <summary>Translates <c>Area:Category:Name</c> keys for the current UI culture.</summary>
public interface ILocalizer
{
    string this[string key, params object[] arguments] { get; }
}

/// <summary>
/// Reads <c>Resources/ar.json</c> and <c>Resources/en.json</c> embedded in every Majlis assembly and merges them.
/// Arabic is the default culture.
/// </summary>
public sealed class JsonLocalizer : ILocalizer
{
    public const string DefaultCulture = "ar";
    public static readonly string[] SupportedCultures = ["ar", "en"];

    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new();

    public string this[string key, params object[] arguments]
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var table = Cache.GetOrAdd(SupportedCultures.Contains(culture) ? culture : DefaultCulture, Load);
            var text = table.TryGetValue(key, out var value) ? value : key;
            return arguments.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, arguments);
        }
    }

    private static IReadOnlyDictionary<string, string> Load(string culture)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        var suffix = $".Resources.{culture}.json";
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => a.GetName().Name?.StartsWith("Majlis.", StringComparison.Ordinal) == true))
        {
            foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(suffix, StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
                foreach (var (k, v) in entries)
                {
                    merged[k] = v;
                }
            }
        }

        return merged;
    }

    /// <summary>Test hook: forget loaded tables (e.g. after loading another assembly).</summary>
    public static void ResetCache() => Cache.Clear();
}
