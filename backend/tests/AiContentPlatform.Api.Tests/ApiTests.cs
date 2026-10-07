using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AiContentPlatform.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ai-content-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("Ai:TextProvider", "Mock");
        builder.UseSetting("Ai:ImageProvider", "Mock");
        builder.UseSetting("Ai:Mock:StreamDelayMs", "0");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_dbPath);
    }
}

public class ApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public ApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReportsProviders()
    {
        var health = await _client.GetFromJsonAsync<JsonElement>("/api/health", Ct);

        Assert.Equal("ok", health.GetProperty("status").GetString());
        Assert.Equal("Mock", health.GetProperty("textProvider").GetString());
    }

    [Fact]
    public async Task GenerateContent_ReturnsContentWithSeoScores()
    {
        var response = await _client.PostAsJsonAsync("/api/content/generate", new GenerateContentRequest
        {
            Prompt = "coffee subscriptions",
            Type = ContentType.BlogPost,
            Keywords = ["coffee"]
        }, Json, Ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(Json, Ct);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Title));
        Assert.Contains("coffee", result.Body, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.KeywordScores!["coffee"] > 0);
        Assert.True(result.WordCount > 50);
        Assert.Equal("Mock", result.Provider);
    }

    [Fact]
    public async Task GenerateContentStream_StreamsDeltasThenDoneEvent()
    {
        var response = await _client.PostAsJsonAsync("/api/content/generate/stream", new GenerateContentRequest
        {
            Prompt = "coffee subscriptions",
            Type = ContentType.SocialPost,
            Keywords = ["coffee"]
        }, Json, Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var events = new List<(string Type, JsonElement Data)>();
        await using var stream = await response.Content.ReadAsStreamAsync(Ct);
        await foreach (var item in System.Net.ServerSentEvents.SseParser.Create(stream).EnumerateAsync(Ct))
        {
            events.Add((item.EventType, JsonDocument.Parse(item.Data).RootElement.Clone()));
        }

        var deltas = events.Where(e => e.Type == "delta").Select(e => e.Data.GetProperty("text").GetString()).ToList();
        Assert.True(deltas.Count > 5);
        Assert.StartsWith("# ", string.Concat(deltas));

        var done = Assert.Single(events, e => e.Type == "done").Data;
        Assert.Equal("done", events[^1].Type);
        Assert.Equal("Mock", done.GetProperty("provider").GetString());
        Assert.Equal(string.Concat(deltas)[2..string.Concat(deltas).IndexOf('\n')], done.GetProperty("title").GetString());
        Assert.True(done.GetProperty("keywordScores").GetProperty("coffee").GetDouble() > 0);
    }

    [Fact]
    public async Task GenerateContentStream_WithoutPrompt_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/content/generate/stream", new { prompt = "" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateContent_WithoutPrompt_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/content/generate", new { prompt = "" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateImage_ReturnsSvgDataUri()
    {
        var response = await _client.PostAsJsonAsync("/api/image/generate", new GenerateImageRequest { Prompt = "A <sunny> day" }, Ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GenerateImageResponse>(Ct);
        Assert.StartsWith("data:image/svg+xml;base64,", result!.Url);
        var svg = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(result.Url.Split(',')[1]));
        Assert.Contains("A &lt;sunny&gt; day", svg);
    }

    [Fact]
    public async Task Projects_SeededDemoProjectExists()
    {
        var projects = await _client.GetFromJsonAsync<List<ProjectDto>>("/api/projects", Json, Ct);

        Assert.Contains(projects!, p => p.Name == "Acme Coffee Launch" && p.ContentCount == 2);
    }

    [Fact]
    public async Task ProjectAndContent_FullCrudFlow()
    {
        // Create project
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new SaveProjectRequest { Name = "Test project" }, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var project = (await createResponse.Content.ReadFromJsonAsync<ProjectDto>(Json, Ct))!;

        // Add content
        var addResponse = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/content", new SaveContentItemRequest
        {
            Type = ContentType.SocialPost,
            Title = "Hello",
            Body = "World",
            Keywords = ["a", "A", " b "]
        }, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var item = (await addResponse.Content.ReadFromJsonAsync<ContentItemDto>(Json, Ct))!;
        Assert.Equal(["a", "b"], item.Keywords);

        // Update content
        var updateResponse = await _client.PutAsJsonAsync($"/api/content-items/{item.Id}", new SaveContentItemRequest
        {
            Type = ContentType.SocialPost,
            Title = "Hello again",
            Body = "Updated"
        }, Json, Ct);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content.ReadFromJsonAsync<ContentItemDto>(Json, Ct))!;
        Assert.Equal("Hello again", updated.Title);
        Assert.NotNull(updated.UpdatedAt);

        // List content
        var items = await _client.GetFromJsonAsync<List<ContentItemDto>>($"/api/projects/{project.Id}/content", Json, Ct);
        Assert.Single(items!);

        // Delete content and project
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/content-items/{item.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/content-items/{item.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/projects/{project.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/projects/{project.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task AddContent_ToUnknownProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/content",
            new SaveContentItemRequest { Title = "t", Body = "b" }, Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns404()
    {
        var response = await _client.GetAsync("/api/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
