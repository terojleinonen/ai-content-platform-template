using System.Text.RegularExpressions;

namespace AiContentPlatform.Api.Services;

public enum TextLanguage
{
    /// <summary>Detect from the text itself.</summary>
    Auto,
    English,
    Finnish,
    Other
}

/// <summary>
/// Lightweight language helpers: detection by common function words (good enough to pick a
/// word-matching strategy, not a general-purpose detector) and normalizing user-entered names.
/// </summary>
public static partial class Languages
{
    private static readonly HashSet<string> FinnishWords =
    [
        "ja", "on", "ei", "että", "se", "ovat", "kun", "myös", "tai", "mutta", "joka", "jotka", "kanssa", "nyt",
        "vain", "jo", "niin", "kuin", "tämä", "tässä", "sinun", "meidän", "olet", "ole", "voit", "jos", "sekä", "kaikki"
    ];

    private static readonly HashSet<string> EnglishWords =
    [
        "the", "and", "is", "to", "of", "in", "that", "it", "for", "you", "with", "on", "are", "your", "this",
        "be", "as", "at", "or", "from", "our", "we", "an", "by", "can", "will"
    ];

    // Case endings, possessives and clitics that are very common in Finnish and rare as English word endings.
    private static readonly string[] FinnishEndings =
    [
        "aan", "ään", "ssa", "ssä", "sta", "stä", "lla", "llä", "lta", "ltä", "lle", "ksi", "tta", "ttä",
        "ua", "yä", "ia", "iä", "esi", "nsa", "nsä", "mme", "nne", "kin", "kaan", "kään"
    ];

    [GeneratedRegex(@"[\p{L}]+")]
    private static partial Regex WordRegex();

    public static TextLanguage Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return TextLanguage.Other;

        var words = WordRegex().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();
        if (words.Count == 0) return TextLanguage.Other;

        var finnish = words.Count(FinnishWords.Contains);
        var english = words.Count(EnglishWords.Contains);
        // ä/ö are frequent in Finnish and rare in English; this helps short texts with few function words.
        var withUmlauts = words.Count(w => w.Contains('ä') || w.Contains('ö'));
        var withFinnishEndings = words.Count(w => w.Length > 4 && FinnishEndings.Any(e => w.EndsWith(e, StringComparison.Ordinal)));
        finnish += (withUmlauts + withFinnishEndings) / 2;

        if (finnish >= 2 && finnish > english) return TextLanguage.Finnish;
        if (english >= 2 && english >= finnish) return TextLanguage.English;
        return TextLanguage.Other;
    }

    /// <summary>Maps a user-entered language ("Finnish", "fi", "suomi", ...) to a known language.</summary>
    public static TextLanguage Parse(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        null or "" => TextLanguage.Auto,
        "en" or "eng" or "english" or "en-us" or "en-gb" => TextLanguage.English,
        "fi" or "fin" or "finnish" or "suomi" or "fi-fi" => TextLanguage.Finnish,
        _ => TextLanguage.Other
    };

    /// <summary>True when content in this language needs no translation of English terms.</summary>
    public static bool IsEnglishOrUnspecified(string? name) => Parse(name) is TextLanguage.English or TextLanguage.Auto;
}
