namespace AiContentPlatform.Api.Services;

/// <summary>
/// Decides whether a word in a text is an inflected form of a keyword word.
/// Finnish: stem-based matching that tolerates case endings and common consonant gradation
/// (Helsinki → Helsingissä, kauppa → kaupassa, ihminen → ihmisen, kokemus → kokemuksen).
/// English: exact match plus regular plurals (bag → bags, story → stories).
/// </summary>
public static class WordMatcher
{
    private const string Vowels = "aeiouyäö";

    // Longest inflectional ending (case + plural + possessive + clitic) we allow after a stem.
    private const int MaxFinnishSuffix = 8;

    // Strong → weak grade of the consonant(s) before the last vowel.
    private static readonly (string Strong, string Weak)[] Gradation =
    [
        ("kk", "k"), ("pp", "p"), ("tt", "t"), ("nk", "ng"), ("mp", "mm"), ("nt", "nn"), ("lt", "ll"),
        ("rt", "rr"), ("lk", "l"), ("rk", "r"), ("hk", "h"), ("p", "v"), ("t", "d"), ("k", "")
    ];

    public static Func<string, bool> For(string keywordWord, TextLanguage language)
    {
        var keyword = keywordWord.ToLowerInvariant();
        return language == TextLanguage.Finnish ? FinnishMatcher(keyword) : EnglishMatcher(keyword);
    }

    private static Func<string, bool> EnglishMatcher(string keyword)
    {
        var forms = new HashSet<string> { keyword, keyword + "s", keyword + "es" };
        if (keyword.Length > 2 && keyword[^1] == 'y' && !Vowels.Contains(keyword[^2]))
        {
            forms.Add(keyword[..^1] + "ies");
        }
        return forms.Contains;
    }

    private static Func<string, bool> FinnishMatcher(string keyword)
    {
        // Short words inflect unpredictably and stems this short would match far too much.
        if (keyword.Length < 4) return word => word == keyword;

        var stems = FinnishStems(keyword);
        return word => word == keyword ||
            stems.Any(stem => word.StartsWith(stem, StringComparison.Ordinal) && word.Length - stem.Length <= MaxFinnishSuffix);
    }

    internal static IReadOnlyCollection<string> FinnishStems(string word)
    {
        var minLength = Math.Max(3, (word.Length + 1) / 2);
        var stems = new HashSet<string>();
        void Add(string stem)
        {
            if (stem.Length >= minLength) stems.Add(stem);
        }

        var stem = word.TrimEnd(Vowels.ToCharArray());
        if (stem.Length < minLength) stem = word;
        Add(stem);

        // Strong grade in the base form, weak grade when inflected: kauppa → kaupa-ssa, Helsinki → Helsingi-ssä.
        foreach (var (strong, weak) in Gradation)
        {
            if (stem.EndsWith(strong, StringComparison.Ordinal)) Add(stem[..^strong.Length] + weak);
        }

        // e-words go the other way, weak in the base form: tunne → tunte-en, liike → liikke-en.
        if (word.EndsWith('e'))
        {
            foreach (var (strong, weak) in Gradation)
            {
                if (weak.Length > 0 && stem.EndsWith(weak, StringComparison.Ordinal)) Add(stem[..^weak.Length] + strong);
            }
        }

        // -nen → -s-: ihminen → ihmisen, suomalainen → suomalaisia.
        if (word.EndsWith("nen", StringComparison.Ordinal)) Add(word[..^3] + "s");

        // -us/-ys/-os/-ös → -uks-/-ude-: kokemus → kokemuksen, rakkaus → rakkauden.
        if (word.Length > 4 && word[^1] == 's' && "uyoö".Contains(word[^2]))
        {
            Add(word[..^1] + "ks");
            Add(word[..^1] + "d");
        }

        return stems;
    }
}
