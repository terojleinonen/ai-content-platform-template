using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

public interface ISeoScoringService
{
    /// <summary>
    /// Counts each keyword and rates it for the content's type and length: by mentions for short
    /// text, by density for longer text. Multi-word keywords are matched as phrases, and inflected
    /// forms count (Finnish case endings, English plurals). The language is detected unless given.
    /// </summary>
    SeoReport Analyze(string content, IEnumerable<string> keywords, ContentType? type = null, TextLanguage language = TextLanguage.Auto);

    /// <summary>Keyword → density map (0..1), with the same matching as <see cref="Analyze"/>.</summary>
    Dictionary<string, double> ScoreKeywords(string content, IEnumerable<string> keywords, TextLanguage language = TextLanguage.Auto);

    int CountWords(string content);
}
