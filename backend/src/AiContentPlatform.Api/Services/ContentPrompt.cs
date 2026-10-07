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

    public const string EditSystemPrompt = """
        You are an expert editor and SEO copywriter for a content marketing platform.
        You revise existing content according to the editing task, preserving its facts and intent.
        Never invent statistics, quotes or facts that aren't in the original.
        Keep the requested SEO keywords in the text, used naturally.
        Respond in Markdown with the complete revised piece. The first line MUST be the title as a
        level-1 heading ("# Title"). Output only the content itself: no preamble, no notes, no code fences.
        """;

    // Distinct angles so parallel variants don't all come out the same.
    private static readonly string[] VariantAngles =
    [
        "Take a direct, practical how-to angle.",
        "Open with a short, relatable story or scenario.",
        "Lead with a bold, surprising insight and a punchy title."
    ];

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
        if (request.Variant > 0)
            sb.AppendLine($"Angle: {VariantAngles[(request.Variant - 1) % VariantAngles.Length]}");

        return sb.ToString().TrimEnd();
    }

    public static string BuildTransformPrompt(TransformContentRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Editing task: {DescribeTask(request)}");
        sb.AppendLine($"Content type: {Describe(request.Type)}");

        var keywords = request.Keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToArray();
        if (keywords is { Length: > 0 })
            sb.AppendLine($"SEO keywords to keep: {string.Join(", ", keywords)}");

        sb.AppendLine();
        sb.AppendLine("Content to edit:");
        sb.AppendLine("<content>");
        if (!string.IsNullOrWhiteSpace(request.Title))
            sb.AppendLine($"# {request.Title.Trim()}").AppendLine();
        sb.AppendLine(request.Body.Trim());
        sb.AppendLine("</content>");

        return sb.ToString().TrimEnd();
    }

    private static string DescribeTask(TransformContentRequest request) => request.Action switch
    {
        TransformAction.Improve =>
            "Improve clarity, flow and engagement, and fix grammar and awkward phrasing. Keep the meaning, structure, language and roughly the same length.",
        TransformAction.Shorten =>
            "Make it about 50% shorter. Keep the key points, the structure and the language.",
        TransformAction.Expand =>
            "Expand it by about 50% with more depth, explanation and concrete examples. Keep the structure and the language.",
        TransformAction.ChangeTone =>
            $"Rewrite it in a {request.ToneOfVoice!.Trim()} tone of voice. Keep the content, structure and language.",
        TransformAction.Translate =>
            $"Translate it, including the title, into {request.Language!.Trim()}. Adapt idioms naturally and keep the Markdown formatting.",
        _ =>
            $"Apply this instruction: {request.Instruction!.Trim()}"
    };

    /// <summary>
    /// Splits the model output into a title (leading "# " heading) and body.
    /// </summary>
    public static GeneratedText Parse(string output, string? fallbackTitle)
    {
        var text = StripCodeFence(output.Trim());
        fallbackTitle = string.IsNullOrWhiteSpace(fallbackTitle) ? "Untitled" : fallbackTitle.Trim();

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
