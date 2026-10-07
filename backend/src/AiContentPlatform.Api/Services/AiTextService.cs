using System.Text;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Orchestrates content generation and editing: delegates writing to the configured provider,
/// then enriches the result with SEO metrics and a brand check in the content's language.
/// </summary>
public class AiTextService : IAiTextService
{
    private static readonly TimeSpan TermCacheDuration = TimeSpan.FromHours(12);

    private readonly ITextGenerationProvider _provider;
    private readonly ISeoScoringService _seo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AiTextService> _logger;

    public AiTextService(ITextGenerationProvider provider, ISeoScoringService seo, IMemoryCache cache, ILogger<AiTextService> logger)
    {
        _provider = provider;
        _seo = seo;
        _cache = cache;
        _logger = logger;
    }

    public string ProviderName => _provider.Name;

    public async Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        await LocalizeTermsAsync(request, cancellationToken);
        return BuildResponse(await CollectAsync(_provider.StreamContentAsync(request, cancellationToken)), request);
    }

    public IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default) =>
        _provider.StreamContentAsync(request, cancellationToken);

    public async Task<IReadOnlyList<GenerateContentResponse>> GenerateVariantsAsync(GenerateContentRequest request, int count, CancellationToken cancellationToken = default)
    {
        // Localize once so all variants share the same translated terms.
        await LocalizeTermsAsync(request, cancellationToken);
        var tasks = Enumerable.Range(1, count).Select(async i =>
            BuildResponse(await CollectAsync(_provider.StreamContentAsync(request.AsVariant(i), cancellationToken)), request));
        return await Task.WhenAll(tasks);
    }

    public async Task<GenerateContentResponse> TransformContentAsync(TransformContentRequest request, CancellationToken cancellationToken = default)
    {
        await LocalizeTermsAsync(request, cancellationToken);
        return BuildResponse(await CollectAsync(_provider.StreamTransformAsync(request, cancellationToken)), request);
    }

    public IAsyncEnumerable<string> StreamTransformAsync(TransformContentRequest request, CancellationToken cancellationToken = default) =>
        _provider.StreamTransformAsync(request, cancellationToken);

    public async Task LocalizeTermsAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TermsLocalized) return;
        request.TermsLocalized = true;

        var language = Languages.IsEnglishOrUnspecified(request.Language) ? null : request.Language!.Trim();
        (request.Keywords, request.Brand, request.TermTranslations) =
            await LocalizeAsync(request.Keywords, request.Brand, language, cancellationToken);
    }

    public async Task LocalizeTermsAsync(TransformContentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TermsLocalized) return;
        request.TermsLocalized = true;

        // Translating: terms follow the target language. Other edits keep the text's language,
        // so terms only need translating when the text being edited is Finnish.
        var language = request.Action == TransformAction.Translate
            ? request.Language!.Trim()
            : Languages.Detect(request.Body) == TextLanguage.Finnish ? "Finnish" : null;
        (request.Keywords, request.Brand, request.TermTranslations) =
            await LocalizeAsync(request.Keywords, request.Brand, language, cancellationToken);
    }

    public GenerateContentResponse BuildResponse(string output, GenerateContentRequest request) =>
        BuildResponse(output, request.Title, request.Keywords, request.Brand, request.TermTranslations,
            Languages.Parse(request.Language));

    public GenerateContentResponse BuildResponse(string output, TransformContentRequest request) =>
        BuildResponse(output, request.Title, request.Keywords, request.Brand, request.TermTranslations,
            request.Action == TransformAction.Translate ? Languages.Parse(request.Language) : TextLanguage.Auto);

    private GenerateContentResponse BuildResponse(
        string output, string? fallbackTitle, IEnumerable<string>? keywords, BrandContext? brand,
        Dictionary<string, string>? translations, TextLanguage requestedLanguage)
    {
        var generated = ContentPrompt.Parse(output, fallbackTitle);
        var language = requestedLanguage == TextLanguage.Finnish ? TextLanguage.Finnish : Languages.Detect(generated.Body);
        var cleanKeywords = CleanTerms(keywords);

        return new GenerateContentResponse
        {
            Title = generated.Title,
            Body = generated.Body,
            SeoSummary = _seo.BuildSeoSummary(generated.Body, cleanKeywords, language),
            KeywordScores = _seo.ScoreKeywords(generated.Body, cleanKeywords, language),
            WordCount = _seo.CountWords(generated.Body),
            Provider = _provider.Name,
            BrandCheck = CheckBrand(generated, brand, language),
            TermTranslations = translations is { Count: > 0 } ? translations : null
        };
    }

    /// <summary>
    /// Translates keywords and brand terms into <paramref name="language"/> and returns the
    /// localized keywords and brand. Preferred terms are replaced by their translations; avoided
    /// terms are kept in both languages, so neither version slips through.
    /// </summary>
    private async Task<(string[]? Keywords, BrandContext? Brand, Dictionary<string, string>? Translations)> LocalizeAsync(
        string[]? keywords, BrandContext? brand, string? language, CancellationToken cancellationToken)
    {
        var cleanKeywords = CleanTerms(keywords);
        var terms = cleanKeywords
            .Concat(brand?.Voice.PreferredTerms ?? [])
            .Concat(brand?.Voice.AvoidTerms ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (language is null || terms.Count == 0) return (keywords, brand, null);

        var map = await TranslateTermsAsync(terms, language, cancellationToken);
        string Translate(string term) => map.GetValueOrDefault(term, term);

        var localizedBrand = brand is null ? null : brand with
        {
            Voice = new BrandVoice
            {
                Voice = brand.Voice.Voice,
                TargetAudience = brand.Voice.TargetAudience,
                KeyFacts = brand.Voice.KeyFacts,
                PreferredTerms = Distinct(brand.Voice.PreferredTerms.Select(Translate)),
                AvoidTerms = Distinct(brand.Voice.AvoidTerms.Concat(brand.Voice.AvoidTerms.Select(Translate)))
            }
        };
        var changed = map
            .Where(t => !string.Equals(t.Key, t.Value, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);

        return (Distinct(cleanKeywords.Select(Translate)).ToArray(), localizedBrand, changed.Count > 0 ? changed : null);
    }

    /// <summary>
    /// Translates terms, using cached translations where possible. Never fails the generation:
    /// on provider errors the original terms are used.
    /// </summary>
    private async Task<Dictionary<string, string>> TranslateTermsAsync(List<string> terms, string language, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var term in terms)
        {
            if (_cache.TryGetValue(CacheKey(language, term), out string? cached)) map[term] = cached!;
            else missing.Add(term);
        }

        if (missing.Count == 0) return map;

        try
        {
            var translated = await _provider.TranslateTermsAsync(missing, language, cancellationToken);
            for (var i = 0; i < missing.Count; i++)
            {
                map[missing[i]] = translated[i];
                _cache.Set(CacheKey(language, missing[i]), translated[i], TermCacheDuration);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not translate {Count} terms into {Language}; using the originals", missing.Count, language);
            foreach (var term in missing) map[term] = term;
        }

        return map;
    }

    private string CacheKey(string language, string term) =>
        $"terms:{_provider.Name}:{language.ToLowerInvariant()}:{term.ToLowerInvariant()}";

    /// <summary>Deterministic check of avoided and preferred terms (phrase- and inflection-aware).</summary>
    private BrandCheckResult? CheckBrand(GeneratedText generated, BrandContext? brand, TextLanguage language)
    {
        if (brand is null || (brand.Voice.AvoidTerms.Count == 0 && brand.Voice.PreferredTerms.Count == 0))
        {
            return null;
        }

        var text = generated.Title + "\n" + generated.Body;
        var avoid = _seo.ScoreKeywords(text, brand.Voice.AvoidTerms, language);
        var preferred = _seo.ScoreKeywords(text, brand.Voice.PreferredTerms, language);

        return new BrandCheckResult(
            brand.ProjectName,
            avoid.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList(),
            preferred.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList(),
            preferred.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList());
    }

    private static string[] CleanTerms(IEnumerable<string>? terms) =>
        (terms ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToArray();

    private static List<string> Distinct(IEnumerable<string> terms) =>
        terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private async Task<string> CollectAsync(IAsyncEnumerable<string> chunks)
    {
        var output = new StringBuilder();
        await foreach (var chunk in chunks)
        {
            output.Append(chunk);
        }

        if (string.IsNullOrWhiteSpace(output.ToString()))
        {
            throw new AiProviderException(_provider.Name, "Empty response.");
        }

        return output.ToString();
    }
}
