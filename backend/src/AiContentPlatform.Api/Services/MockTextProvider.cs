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

    public IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default) =>
        StreamWordsAsync(Generate(request), cancellationToken);

    public IAsyncEnumerable<string> StreamTransformAsync(TransformContentRequest request, CancellationToken cancellationToken = default) =>
        StreamWordsAsync(Transform(request), cancellationToken);

    private async IAsyncEnumerable<string> StreamWordsAsync(GeneratedText generated, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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
        var title = string.IsNullOrWhiteSpace(request.Title) ? MakeTitle(topic, request.Type, request.Variant) : request.Title.Trim();
        var audience = string.IsNullOrWhiteSpace(request.TargetAudience) ? "readers" : request.TargetAudience.Trim();
        var keywords = request.Keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList() ?? new();
        var kw = (int i) => keywords.Count == 0 ? Decapitalize(topic) : keywords[i % keywords.Count];
        var opener = Opener(request.ToneOfVoice, request.Variant);

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

    /// <summary>
    /// Simulated edits. They are rule-based, so they only approximate what a real model does:
    /// enough to demo the editing flow offline.
    /// </summary>
    internal static GeneratedText Transform(TransformContentRequest request)
    {
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title.Trim();
        var blocks = request.Body.Trim().Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .ToList();

        switch (request.Action)
        {
            case TransformAction.Shorten:
                blocks = blocks.Select(b => UnderHeading(b, ShortenBlock)).ToList();
                break;

            case TransformAction.Expand:
                var extras = ExpansionSentences;
                blocks = blocks.Select((b, i) => UnderHeading(b, p => IsProse(p) ? $"{p} {extras[i % extras.Length]}" : p)).ToList();
                break;

            case TransformAction.Improve:
                blocks = blocks.Select(Improve).ToList();
                break;

            case TransformAction.ChangeTone:
                var first = blocks.FindIndex(IsProse);
                if (first >= 0)
                {
                    var paragraph = KnownOpeners.Aggregate(blocks[first], (p, o) => p.StartsWith(o) ? p[o.Length..].TrimStart() : p);
                    blocks[first] = $"{Opener(request.ToneOfVoice, 0)} {paragraph}";
                }
                break;

            case TransformAction.Translate:
                title = $"[{request.Language!.Trim()}] {title}";
                blocks.Insert(0, $"_(The mock provider can't translate — configure a real AI provider to translate into {request.Language.Trim()}.)_");
                break;

            case TransformAction.Custom:
                blocks.Add($"_(The mock provider can't follow custom instructions — configure a real AI provider to apply: “{request.Instruction!.Trim()}”.)_");
                break;
        }

        return new GeneratedText(title, string.Join("\n\n", blocks));
    }

    private static readonly string[] ExpansionSentences =
    [
        "In practice, this means starting with one small, concrete step and building from there.",
        "For example, a team that reviews its results every week can spot what works long before a quarterly report would.",
        "It's worth revisiting this regularly, because what works today may need adjusting as your audience grows."
    ];

    private static readonly (string From, string To)[] Improvements =
    [
        ("very important", "essential"), ("very good", "excellent"), ("a lot of", "many"),
        ("in order to", "to"), ("really ", ""), ("just ", ""), ("doesn't have to be", "needn't be")
    ];

    private static string[] KnownOpeners =>
        ["Guess what? 🎉", "We are pleased to share an update.", "Hey there! 👋", "Stop settling for less.",
         "Here's something worth your attention.", "Here's the short version.", "Picture this."];

    /// <summary>Applies <paramref name="edit"/> to a block, or to the text under its "## heading" line.</summary>
    private static string UnderHeading(string block, Func<string, string> edit)
    {
        if (!block.StartsWith('#')) return edit(block);

        var newline = block.IndexOf('\n');
        return newline < 0 ? block : block[..(newline + 1)] + edit(block[(newline + 1)..].Trim());
    }

    private static bool IsProse(string block) =>
        !block.StartsWith('#') && !block.StartsWith("- ") && !block.StartsWith("* ") && !block.StartsWith("**") && !block.StartsWith("_(");

    private static string ShortenBlock(string block)
    {
        var lines = block.Split('\n');
        if (lines.All(l => l.TrimStart().StartsWith("- ") || l.StartsWith("**")))
        {
            // Lists: keep the label line (if any) and the first two items.
            return string.Join('\n', lines.Where(l => l.StartsWith("**")).Concat(lines.Where(l => l.TrimStart().StartsWith("- ")).Take(2)));
        }
        if (!IsProse(block)) return block;

        // Paragraphs: keep the first sentence.
        var end = block.IndexOfAny(['.', '!', '?']);
        return end > 0 && end < block.Length - 1 ? block[..(end + 1)] : block;
    }

    private static string Improve(string block)
    {
        var improved = Improvements.Aggregate(block, (b, r) => b.Replace(r.From, r.To, StringComparison.OrdinalIgnoreCase));
        return Regex.Replace(improved, " {2,}", " ");
    }

    private static string MakeTitle(string topic, ContentType type, int variant)
    {
        var t = Capitalize(topic.Length > 80 ? topic[..80].TrimEnd() + "…" : topic);
        return type switch
        {
            ContentType.BlogPost => (variant % 3) switch
            {
                2 => $"The Story Behind {t}",
                0 when variant > 0 => $"{t}: What Nobody Tells You",
                _ => $"{t}: A Practical Guide"
            },
            ContentType.Email => $"Introducing: {t}",
            ContentType.SocialPost => t,
            _ => t
        };
    }

    private static string Opener(string? tone, int variant) => tone?.Trim().ToLowerInvariant() switch
    {
        "playful" or "fun" => "Guess what? 🎉",
        "professional" or "formal" => "We are pleased to share an update.",
        "friendly" or "casual" => "Hey there! 👋",
        "persuasive" or "bold" => "Stop settling for less.",
        _ => (variant % 3) switch
        {
            2 => "Picture this.",
            0 when variant > 0 => "Here's the short version.",
            _ => "Here's something worth your attention."
        }
    };

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];

    private static string Decapitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLower(s[0], CultureInfo.InvariantCulture) + s[1..];

    private static string Hashtag(string s) =>
        "#" + string.Concat(s.Split(' ', '-', '_').Where(p => p.Length > 0).Select(Capitalize));
}
