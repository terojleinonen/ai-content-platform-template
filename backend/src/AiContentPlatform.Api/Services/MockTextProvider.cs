using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Offline, template-based generator so the demo works without any API key.
/// Output is deterministic and structured like real model output (Markdown, keywords woven in),
/// and is streamed word by word to simulate a model typing.
/// </summary>
public partial class MockTextProvider : ITextGenerationProvider
{
    private readonly int _streamDelayMs;

    public MockTextProvider(IOptions<AiOptions> options)
    {
        _streamDelayMs = options.Value.Mock.StreamDelayMs;
    }

    public string Name => AiProviderNames.Mock;

    // Words with their trailing whitespace, so chunks concatenate back to the original text.
    [GeneratedRegex(@"\S+\s*|\s+")]
    private static partial Regex ChunkRegex();

    public async IAsyncEnumerable<string> StreamAsync(GenerateContentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var generated = Generate(request);
        var markdown = $"# {generated.Title}\n\n{generated.Body}";

        foreach (Match chunk in ChunkRegex().Matches(markdown))
        {
            if (_streamDelayMs > 0)
            {
                await Task.Delay(_streamDelayMs, cancellationToken);
            }
            yield return chunk.Value;
        }
    }

    internal static GeneratedText Generate(GenerateContentRequest request)
    {
        var topic = request.Prompt.Trim().TrimEnd('.', '!', '?');
        var title = string.IsNullOrWhiteSpace(request.Title) ? MakeTitle(topic, request.Type) : request.Title.Trim();
        var audience = string.IsNullOrWhiteSpace(request.TargetAudience) ? "readers" : request.TargetAudience.Trim();
        var keywords = request.Keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList() ?? new();
        var kw = (int i) => keywords.Count == 0 ? Decapitalize(topic) : keywords[i % keywords.Count];
        var opener = Opener(request.ToneOfVoice);

        var body = request.Type switch
        {
            ContentType.SocialPost =>
                $"{opener} {Capitalize(topic)} — made for {audience}. " +
                $"Discover why everyone is talking about {kw(0)}. 👉 Learn more today! " +
                string.Join(' ', (keywords.Count > 0 ? keywords : new List<string> { topic }).Take(3).Select(Hashtag)),

            ContentType.ProductDescription => $"""
                {opener} {Capitalize(topic)} is designed for {audience} who expect more.

                Built around {kw(0)}, it combines thoughtful design with everyday practicality, so you get results from day one.

                **Key benefits**
                - Effortless {kw(1)} without the usual trade-offs
                - Premium quality you can see and feel
                - Backed by our 30-day satisfaction guarantee

                Upgrade your routine with {kw(0)} — order today.
                """,

            ContentType.Email => $"""
                Hi there,

                {opener} We wanted you to be the first to hear about {Decapitalize(topic)}.

                As one of our valued {audience}, you get early access to everything {kw(0)} has to offer. We built it to solve the problems you told us about — and we think you'll love the result.

                **What's in it for you**
                - Exclusive early-bird pricing
                - Hands-on guidance on {kw(1)}
                - Priority support from our team

                👉 Claim your spot today — the offer ends Friday.

                Best regards,
                The Team
                """,

            _ => $"""
                {opener} In this post we explore {Decapitalize(topic)} and what it means for {audience}.

                ## Why {Capitalize(kw(0))} matters
                {Capitalize(kw(0))} has quickly become a priority for teams that want measurable results. Understanding the fundamentals helps you make better decisions and avoid costly mistakes.

                ## Getting started with {kw(1)}
                Start small: define a clear goal, pick one metric to track, and iterate weekly. Most successful {audience} treat {kw(1)} as an ongoing habit rather than a one-off project.

                ## Common pitfalls to avoid
                - Trying to do everything at once
                - Ignoring feedback from your audience
                - Measuring vanity metrics instead of outcomes

                ## Conclusion
                {Capitalize(topic)} doesn't have to be complicated. With a focused plan and the right tools for {kw(0)}, you can see progress within weeks.
                """
        };

        // Reference the language so it's visible that the mock ignores it.
        if (!string.IsNullOrWhiteSpace(request.Language) &&
            !request.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) &&
            !request.Language.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            body += $"\n\n_(Mock provider writes English only — configure a real AI provider for {request.Language.Trim()}.)_";
        }

        return new GeneratedText(title, body.Trim());
    }

    private static string MakeTitle(string topic, ContentType type)
    {
        var t = Capitalize(topic.Length > 80 ? topic[..80].TrimEnd() + "…" : topic);
        return type switch
        {
            ContentType.BlogPost => $"{t}: A Practical Guide",
            ContentType.Email => $"Introducing: {t}",
            ContentType.SocialPost => t,
            _ => t
        };
    }

    private static string Opener(string? tone) => tone?.Trim().ToLowerInvariant() switch
    {
        "playful" or "fun" => "Guess what? 🎉",
        "professional" or "formal" => "We are pleased to share an update.",
        "friendly" or "casual" => "Hey there! 👋",
        "persuasive" or "bold" => "Stop settling for less.",
        _ => "Here's something worth your attention."
    };

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];

    private static string Decapitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLower(s[0], CultureInfo.InvariantCulture) + s[1..];

    private static string Hashtag(string s) =>
        "#" + string.Concat(s.Split(' ', '-', '_').Where(p => p.Length > 0).Select(Capitalize));
}
