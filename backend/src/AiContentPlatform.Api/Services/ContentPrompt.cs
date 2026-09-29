using System.Text;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Shared prompt construction and output parsing for LLM-backed text providers.
/// </summary>
public static class ContentPrompt
{
    public const string SystemPrompt = """
        You are an expert content writer and SEO copywriter for a content marketing platform.
        Write original, engaging, well-structured content that follows the brief exactly.
        Weave the requested keywords in naturally; never stuff them.
        Respond in Markdown. The first line MUST be the title as a level-1 heading ("# Title").
        Output only the content itself: no preamble, no notes, no code fences.
        """;

    public static string BuildUserPrompt(GenerateContentRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Content type: {Describe(request.Type)}");
        sb.AppendLine($"Length: {LengthGuidance(request.Type)}");
        sb.AppendLine($"Brief: {request.Prompt.Trim()}");

        if (!string.IsNullOrWhiteSpace(request.Title))
            sb.AppendLine($"Title (use exactly): {request.Title.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.TargetAudience))
            sb.AppendLine($"Target audience: {request.TargetAudience.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.ToneOfVoice))
            sb.AppendLine($"Tone of voice: {request.ToneOfVoice.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.Language))
            sb.AppendLine($"Language: {request.Language.Trim()}");

        var keywords = request.Keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToArray();
        if (keywords is { Length: > 0 })
            sb.AppendLine($"SEO keywords: {string.Join(", ", keywords)}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Splits the model output into a title (leading "# " heading) and body.
    /// </summary>
    public static GeneratedText Parse(string output, GenerateContentRequest request)
    {
        var text = StripCodeFence(output.Trim());
        var fallbackTitle = string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title.Trim();

        var newline = text.IndexOf('\n');
        var firstLine = (newline < 0 ? text : text[..newline]).Trim();
        if (firstLine.StartsWith("# "))
        {
            var body = newline < 0 ? string.Empty : text[(newline + 1)..].Trim();
            return new GeneratedText(firstLine[2..].Trim(), body);
        }

        return new GeneratedText(fallbackTitle, text);
    }

    public static string Describe(ContentType type) => type switch
    {
        ContentType.BlogPost => "Blog post",
        ContentType.ProductDescription => "Product description",
        ContentType.SocialPost => "Social media post",
        ContentType.Email => "Marketing email",
        _ => "Custom content"
    };

    private static string LengthGuidance(ContentType type) => type switch
    {
        ContentType.BlogPost => "600-900 words with an intro, 3-4 '##' sections and a conclusion",
        ContentType.ProductDescription => "120-200 words, include a short bullet list of key benefits",
        ContentType.SocialPost => "under 280 characters for the body, with 2-3 relevant hashtags",
        ContentType.Email => "150-300 words including a subject-style title, greeting, body and a clear call to action",
        _ => "whatever length best fits the brief"
    };

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```")) return text;

        var firstNewline = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline > 0 && lastFence > firstNewline
            ? text[(firstNewline + 1)..lastFence].Trim()
            : text;
    }
}
