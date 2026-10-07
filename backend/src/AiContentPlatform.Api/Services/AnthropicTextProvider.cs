using System.Runtime.CompilerServices;
using System.Text.Json;
using AiContentPlatform.Api.Options;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Text generation with the official Anthropic C# SDK (streaming Messages API). Uses the beta
/// endpoint for server-side refusal fallbacks; the SDK retries rate limits and server errors.
/// </summary>
public class AnthropicTextProvider : LlmTextProvider
{
    // Models that accept `fallbacks: "default"` (server-side-fallback-2026-07-01).
    private static readonly HashSet<string> DefaultFallbackModels =
        new(StringComparer.OrdinalIgnoreCase) { "claude-fable-5-1", "claude-opus-5-5", "claude-opus-5", "claude-sonnet-5-5" };

    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;

    public AnthropicTextProvider(AnthropicClient client, IOptions<AiOptions> options)
    {
        _client = client;
        _options = options.Value.Anthropic;
    }

    public override string Name => AiProviderNames.Anthropic;

    protected override async IAsyncEnumerable<string> StreamCompletionAsync(
        string systemPrompt, string userPrompt, UsageMeter usage, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var parameters = new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = systemPrompt,
            Messages = [new() { Role = Role.User, Content = userPrompt }],
        };
        if (_options.ServerSideFallback && DefaultFallbackModels.Contains(_options.Model))
        {
            // A safety-classifier decline is re-served by Anthropic's recommended fallback model
            // inside the same stream, instead of failing the request.
            parameters = parameters with { Betas = ["server-side-fallback-2026-07-01"], Fallbacks = new Default() };
        }

        usage.Model = _options.Model;
        string? stopReason = null;
        string? refusalCategory = null;

        await using var events = _client.Beta.Messages.CreateStreaming(parameters, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (await NextAsync(events))
        {
            var streamEvent = events.Current;
            if (streamEvent.TryPickStart(out var start))
            {
                // Names the model that actually serves the request (the fallback model, if one took over).
                usage.Model = start.Message.Model;
                usage.InputTokens = (int)start.Message.Usage.InputTokens;
                usage.CacheWriteTokens = (int)(start.Message.Usage.CacheCreationInputTokens ?? 0);
                usage.CacheReadTokens = (int)(start.Message.Usage.CacheReadInputTokens ?? 0);
                usage.OutputTokens = (int)start.Message.Usage.OutputTokens;
            }
            else if (streamEvent.TryPickDelta(out var delta))
            {
                usage.OutputTokens = (int)delta.Usage.OutputTokens;
                usage.OutputFinal = true;
                stopReason = delta.Delta.StopReason?.Raw();
                refusalCategory = delta.Delta.StopDetails?.Category?.Raw();
            }
            else if (streamEvent.TryPickContentBlockDelta(out var blockDelta) && blockDelta.Delta.TryPickText(out var text))
            {
                yield return text.Text;
            }
        }

        // A decline can arrive before any output or mid-stream; either way the text isn't a result.
        if (stopReason == "refusal")
        {
            throw new AiProviderException(Name, RefusalMessage(refusalCategory));
        }
    }

    /// <summary>Advances the SDK stream, turning SDK errors into provider errors the API reports as 502.</summary>
    private async Task<bool> NextAsync(IAsyncEnumerator<BetaRawMessageStreamEvent> events)
    {
        try
        {
            return await events.MoveNextAsync();
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new AiProviderException(Name, "Rate limit reached (retried). Try again in a moment.", ex);
        }
        catch (AnthropicSseException ex)
        {
            // An `error` event mid-stream, e.g. overloaded_error; its message carries the error JSON.
            throw new AiProviderException(Name, ErrorMessageFrom(ex.Message), ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new AiProviderException(Name, ex.Message, ex);
        }
        catch (AnthropicIOException ex)
        {
            throw new AiProviderException(Name, "Could not reach the API.", ex);
        }
        catch (AnthropicException ex)
        {
            throw new AiProviderException(Name, ex.Message, ex);
        }
    }

    private static string ErrorMessageFrom(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var json = JsonDocument.Parse(text[start..(end + 1)]);
                if (json.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message) && message.GetString() is { Length: > 0 } m)
                {
                    return m;
                }
            }
            catch (JsonException)
            {
                // Not JSON; fall through to the raw text.
            }
        }
        return text;
    }

    private static string RefusalMessage(string? category) => category switch
    {
        "reasoning_extraction" => "Claude declined to write out its internal reasoning. Rephrase the brief without asking for it.",
        null or "" => "Claude declined this request. Try rephrasing the brief.",
        _ => $"Claude declined this request (category: {category}). Try rephrasing the brief."
    };
}
