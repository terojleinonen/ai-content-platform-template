namespace AiContentPlatform.Api.Services;

public interface ISeoScoringService
{
    /// <summary>
    /// Returns a simple keyword -> score map (0..1).
    /// </summary>
    Dictionary<string, double> ScoreKeywords(string content, IEnumerable<string> keywords);

    string BuildSeoSummary(string content, IEnumerable<string> keywords);
}
