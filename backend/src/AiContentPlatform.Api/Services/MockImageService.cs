using System.Net;
using System.Text;
using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Offline image "generator": renders an SVG placeholder whose colors are derived from the prompt,
/// returned as a data: URI so no external service is needed.
/// </summary>
public class MockImageService : IAiImageService
{
    public string Name => AiProviderNames.Mock;

    public Task<GenerateImageResponse> GenerateImageAsync(GenerateImageRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = request.Prompt.Trim();
        var hash = StableHash(prompt + "|" + request.Style);
        var hue1 = (int)(hash % 360);
        var hue2 = (hue1 + 40 + (int)(hash / 360 % 120)) % 360;
        int w = request.Width, h = request.Height;

        var lines = Wrap(prompt, maxChars: Math.Max(12, w / 36), maxLines: 4);
        var fontSize = Math.Max(16, Math.Min(w, h) / 18);
        var startY = h / 2 - (lines.Count - 1) * fontSize * 0.6;

        var svg = new StringBuilder();
        svg.Append($"""<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">""");
        svg.Append($"""<defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="hsl({hue1},70%,55%)"/><stop offset="1" stop-color="hsl({hue2},70%,35%)"/></linearGradient></defs>""");
        svg.Append($"""<rect width="{w}" height="{h}" fill="url(#g)"/>""");
        svg.Append($"""<circle cx="{w * 4 / 5}" cy="{h / 5}" r="{Math.Min(w, h) / 4}" fill="white" fill-opacity="0.12"/>""");
        svg.Append($"""<circle cx="{w * 3 / 20}" cy="{h * 17 / 20}" r="{Math.Min(w, h) * 9 / 50}" fill="black" fill-opacity="0.1"/>""");
        for (var i = 0; i < lines.Count; i++)
        {
            var y = (int)(startY + i * fontSize * 1.2);
            svg.Append($"""<text x="50%" y="{y}" text-anchor="middle" font-family="system-ui, sans-serif" font-size="{fontSize}" font-weight="600" fill="white">{WebUtility.HtmlEncode(lines[i])}</text>""");
        }
        var label = string.IsNullOrWhiteSpace(request.Style) ? "Mock AI image" : $"Mock AI image · {request.Style.Trim()}";
        svg.Append($"""<text x="50%" y="{h - fontSize}" text-anchor="middle" font-family="system-ui, sans-serif" font-size="{fontSize * 11 / 20}" fill="white" fill-opacity="0.75">{WebUtility.HtmlEncode(label)}</text>""");
        svg.Append("</svg>");

        return Task.FromResult(new GenerateImageResponse
        {
            Url = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.ToString())),
            PromptUsed = string.IsNullOrWhiteSpace(request.Style) ? prompt : $"{prompt} (style: {request.Style.Trim()})",
            Provider = Name
        });
    }

    // FNV-1a: string.GetHashCode() is randomized per process, we want stable colors per prompt.
    private static uint StableHash(string s)
    {
        var hash = 2166136261u;
        foreach (var c in s)
        {
            hash = (hash ^ c) * 16777619u;
        }
        return hash;
    }

    private static List<string> Wrap(string text, int maxChars, int maxLines)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > maxChars)
            {
                lines.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) lines.Add(current.ToString());

        if (lines.Count > maxLines)
        {
            lines = lines.Take(maxLines).ToList();
            lines[^1] = lines[^1].TrimEnd('.', ',') + "…";
        }
        return lines;
    }
}
