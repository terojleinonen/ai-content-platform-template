using AiContentPlatform.Api.Domain;

namespace AiContentPlatform.Api.Services;

/// <summary>A project's brand voice, resolved for a generation or edit request.</summary>
public record BrandContext(string ProjectName, BrandVoice Voice);
