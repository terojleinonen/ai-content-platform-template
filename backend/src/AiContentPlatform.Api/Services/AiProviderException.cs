namespace AiContentPlatform.Api.Services;

/// <summary>
/// Thrown when an upstream AI provider fails. Mapped to HTTP 502 by <see cref="AiProviderExceptionHandler"/>.
/// </summary>
public class AiProviderException : Exception
{
    public AiProviderException(string provider, string message, Exception? inner = null)
        : base($"{provider}: {message}", inner)
    {
        Provider = provider;
    }

    public string Provider { get; }
}
