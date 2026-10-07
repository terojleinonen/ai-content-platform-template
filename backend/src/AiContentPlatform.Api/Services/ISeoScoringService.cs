namespace AiContentPlatform.Api.Services;

public interface ISeoScoringService
{
    /// <summary>
    /// Returns a keyword -> density map (0..1). Multi-word keywords are matched as phrases, and
    /// inflected forms count (Finnish case endings, English plurals). The language is detected
    /// from the content unless given.
    /// </summary>
    Dictionary<string, double> ScoreKeywords(string content, IEnumerable<string> keywords, TextLanguage language = TextLanguage.Auto);

    string BuildSeoSummary(string content, IEnumerable<string> keywords, TextLanguage language = TextLanguage.Auto);

    int CountWords(string content);
}
