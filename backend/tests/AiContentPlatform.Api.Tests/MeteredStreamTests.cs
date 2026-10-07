using System.Runtime.CompilerServices;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiContentPlatform.Api.Tests;

public class MeteredStreamTests
{
    /// <summary>Behaves like Anthropic: a provisional output count up front, the final one only at the end.</summary>
    private sealed class ProvisionalUsageProvider : ITextGenerationProvider
    {
        public string Name => "Fake";

        public async IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            request.Usage.Model = "fake-model";
            request.Usage.InputTokens = 200;
            request.Usage.OutputTokens = 1;
            for (var i = 0; i < 50; i++)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested(); // like a cancelled HTTP read
                yield return "forty characters of streamed output text ";
            }
            request.Usage.OutputTokens = 900;
            request.Usage.OutputFinal = true;
        }

        public IAsyncEnumerable<string> StreamTransformAsync(TransformContentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> TranslateTermsAsync(IReadOnlyList<string> terms, string language, UsageMeter usage, CancellationToken cancellationToken = default) =>
            Task.FromResult(terms);
    }

    private sealed class CapturingRecorder : IUsageRecorder
    {
        public readonly List<(UsageContext Context, int Input, int Output, bool Estimated, AiCallStatus Status)> Records = [];

        public double? PriceOf(UsageMeter meter) => null;

        public Task RecordAsync(UsageContext context, string provider, UsageMeter meter, TimeSpan duration, AiCallStatus status)
        {
            Records.Add((context, meter.InputTokens, meter.OutputTokens, meter.Estimated, status));
            return Task.CompletedTask;
        }
    }

    private static (AiTextService Service, CapturingRecorder Recorder) Create()
    {
        var recorder = new CapturingRecorder();
        var service = new AiTextService(new ProvisionalUsageProvider(), new SeoScoringService(),
            new MemoryCache(new MemoryCacheOptions()), recorder, NullLogger<AiTextService>.Instance);
        return (service, recorder);
    }

    [Fact]
    public async Task CompletedStream_RecordsFinalReportedCounts()
    {
        var (service, recorder) = Create();

        await foreach (var _ in service.StreamContentAsync(new GenerateContentRequest { Prompt = "x" }, TestContext.Current.CancellationToken)) { }

        var record = Assert.Single(recorder.Records);
        Assert.Equal((200, 900, false, AiCallStatus.Succeeded), (record.Input, record.Output, record.Estimated, record.Status));
    }

    [Fact]
    public async Task StoppedStream_EstimatesOutputInsteadOfTrustingProvisionalCount()
    {
        var (service, recorder) = Create();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var received = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in service.StreamContentAsync(new GenerateContentRequest { Prompt = "x" }, stop.Token))
            {
                if (++received == 10) stop.Cancel(); // the user presses Stop
            }
        });

        var record = Assert.Single(recorder.Records);
        Assert.Equal(AiCallStatus.Cancelled, record.Status);
        Assert.Equal(200, record.Input);
        Assert.True(record.Estimated);
        Assert.Equal(UsageMeter.EstimateTokens(10 * 41), record.Output); // ~103, not the provisional 1
        Assert.Equal(AiOperation.Generate, record.Context.Operation);
    }
}
