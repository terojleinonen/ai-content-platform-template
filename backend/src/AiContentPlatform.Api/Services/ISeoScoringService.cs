namespace AiContentPlatform.Api.Services;

public interface ISeoScoringService
{
    /// <summary>
    /// Returns a keyword -> density map (0..1). Multi-word keywords are matched as phrases.
    /// </summary>
    Dictionary<string, double> ScoreKeywords(string content, IEnumerable<string> keywords);

    string BuildSeoSummary(string content, IEnumerable<string> keywords);

    int CountWords(string content);
}
