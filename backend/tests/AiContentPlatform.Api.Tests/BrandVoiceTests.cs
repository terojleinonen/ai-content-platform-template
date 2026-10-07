using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;
using Microsoft.Data.Sqlite;

namespace AiContentPlatform.Api.Tests;

public class BrandVoiceTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public BrandVoiceTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<ProjectDto> CreateProjectWithBrandAsync()
    {
        var created = await _client.PostAsJsonAsync("/api/projects", new SaveProjectRequest { Name = $"Brand {Guid.NewGuid():N}" }, Json, Ct);
        var project = (await created.Content.ReadFromJsonAsync<ProjectDto>(Json, Ct))!;

        var saved = await _client.PutAsJsonAsync($"/api/projects/{project.Id}/brand-voice", new SaveBrandVoiceRequest
        {
            Voice = "Warm and friendly",
            TargetAudience = "tea lovers",
            KeyFacts = "Organic tea from Sri Lanka\nShips in 24 hours",
            PreferredTerms = ["organic", "loose-leaf", " organic "],
            AvoidTerms = ["cheap", "readers"]
        }, Json, Ct);
        saved.EnsureSuccessStatusCode();
        return (await saved.Content.ReadFromJsonAsync<ProjectDto>(Json, Ct))!;
    }

    [Fact]
    public async Task SaveBrandVoice_PersistsCleanedValues()
    {
        var project = await CreateProjectWithBrandAsync();

        Assert.Equal(["organic", "loose-leaf"], project.BrandVoice!.PreferredTerms);

        var fetched = await _client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}", Json, Ct);
        Assert.Equal("Warm and friendly", fetched!.BrandVoice!.Voice);
        Assert.Equal(["cheap", "readers"], fetched.BrandVoice.AvoidTerms);
    }

    [Fact]
    public async Task SaveBrandVoice_WithOnlyEmptyFields_RemovesIt()
    {
        var project = await CreateProjectWithBrandAsync();

        var cleared = await _client.PutAsJsonAsync($"/api/projects/{project.Id}/brand-voice", new SaveBrandVoiceRequest { Voice = "  " }, Json, Ct);

        cleared.EnsureSuccessStatusCode();
        Assert.Null((await cleared.Content.ReadFromJsonAsync<ProjectDto>(Json, Ct))!.BrandVoice);
    }

    [Fact]
    public async Task Generate_ForProjectWithBrand_UsesBrandAndReturnsBrandCheck()
    {
        var project = await CreateProjectWithBrandAsync();

        var response = await _client.PostAsJsonAsync("/api/content/generate", new GenerateContentRequest
        {
            Prompt = "our new green tea",
            Type = ContentType.BlogPost,
            ProjectId = project.Id
        }, Json, Ct);

        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<GenerateContentResponse>(Json, Ct))!;

        // The mock uses the brand audience, preferred terms, tone and first key fact.
        Assert.Contains("tea lovers", result.Body);
        Assert.Contains("organic", result.Body);
        Assert.Contains("Hey there!", result.Body);
        Assert.Contains("**Good to know:** Organic tea from Sri Lanka.", result.Body);

        var check = result.BrandCheck!;
        Assert.Equal(project.Name, check.ProjectName);
        Assert.Empty(check.AvoidTermsFound);
        Assert.Contains("organic", check.PreferredTermsUsed);
    }

    [Fact]
    public async Task Generate_WithoutProject_HasNoBrandCheck()
    {
        var response = await _client.PostAsJsonAsync("/api/content/generate", new GenerateContentRequest { Prompt = "tea" }, Json, Ct);

        var result = (await response.Content.ReadFromJsonAsync<GenerateContentResponse>(Json, Ct))!;
        Assert.Null(result.BrandCheck);
    }

    [Fact]
    public async Task Transform_ForProjectWithBrand_FlagsAvoidedTerms()
    {
        var project = await CreateProjectWithBrandAsync();

        var response = await _client.PostAsJsonAsync("/api/content/transform", new TransformContentRequest
        {
            Action = TransformAction.Improve,
            Title = "Tea",
            Body = "Our cheap tea is great.",
            ProjectId = project.Id
        }, Json, Ct);

        var result = (await response.Content.ReadFromJsonAsync<GenerateContentResponse>(Json, Ct))!;
        Assert.Equal(["cheap"], result.BrandCheck!.AvoidTermsFound);
        Assert.Equal(["organic", "loose-leaf"], result.BrandCheck.PreferredTermsMissing);
    }

    [Theory]
    [InlineData("/api/content/generate")]
    [InlineData("/api/content/generate/stream")]
    public async Task Generate_ForUnknownProject_Returns404(string path)
    {
        var response = await _client.PostAsJsonAsync(path, new GenerateContentRequest { Prompt = "tea", ProjectId = Guid.NewGuid() }, Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Prompt_IncludesBrandGuidelines()
    {
        var brand = new BrandContext("Acme", new BrandVoice
        {
            Voice = "Bold",
            KeyFacts = "- Fact one\nFact two",
            AvoidTerms = ["cheap"]
        });

        var prompt = ContentPrompt.BuildUserPrompt(new GenerateContentRequest { Prompt = "x", Brand = brand });

        Assert.Contains("Brand guidelines for \"Acme\"", prompt);
        Assert.Contains("Voice: Bold", prompt);
        Assert.Contains("- Fact one\n- Fact two", prompt.ReplaceLineEndings("\n"));
        Assert.Contains("Never use these words or phrases: cheap", prompt);
    }
}

/// <summary>Databases created by the pre-migrations EnsureCreated startup must upgrade in place.</summary>
public class LegacyDatabaseUpgradeTests
{
    private const string LegacySchema = """
        CREATE TABLE "Users" ("Id" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY, "Email" TEXT NOT NULL, "DisplayName" TEXT NOT NULL, "PasswordHash" TEXT NOT NULL);
        CREATE TABLE "Projects" ("Id" TEXT NOT NULL CONSTRAINT "PK_Projects" PRIMARY KEY, "Name" TEXT NOT NULL, "Description" TEXT NULL, "OwnerId" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL,
            CONSTRAINT "FK_Projects_Users_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE CASCADE);
        CREATE TABLE "ContentItems" ("Id" TEXT NOT NULL CONSTRAINT "PK_ContentItems" PRIMARY KEY, "ProjectId" TEXT NOT NULL, "Type" TEXT NOT NULL, "Title" TEXT NOT NULL, "Body" TEXT NOT NULL,
            "TargetAudience" TEXT NULL, "ToneOfVoice" TEXT NULL, "Keywords" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NULL,
            CONSTRAINT "FK_ContentItems_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE CASCADE);
        CREATE INDEX "IX_ContentItems_ProjectId" ON "ContentItems" ("ProjectId");
        CREATE INDEX "IX_Projects_OwnerId" ON "Projects" ("OwnerId");
        CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
        INSERT INTO "Users" VALUES ('00000000-0000-0000-0000-000000000001', 'demo@example.com', 'Demo User', '');
        INSERT INTO "Projects" VALUES ('11111111-1111-1111-1111-111111111111', 'Legacy project', NULL, '00000000-0000-0000-0000-000000000001', '2026-01-01 00:00:00');
        INSERT INTO "ContentItems" VALUES ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'BlogPost', 'Old post', 'Body', NULL, NULL, '[]', '2026-01-01 00:00:00', NULL);
        """;

    [Fact]
    public async Task Startup_UpgradesLegacyDatabaseAndKeepsData()
    {
        var factory = new ApiFactory();
        await using (var connection = new SqliteConnection($"Data Source={factory.DbPath};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySchema;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using (factory)
        {
            var client = factory.CreateClient();
            var projects = await client.GetFromJsonAsync<List<ProjectDto>>("/api/projects", TestContext.Current.CancellationToken);

            var legacy = Assert.Single(projects!);
            Assert.Equal("Legacy project", legacy.Name);
            Assert.Equal(1, legacy.ContentCount);

            var saved = await client.PutAsJsonAsync($"/api/projects/{legacy.Id}/brand-voice",
                new SaveBrandVoiceRequest { Voice = "Calm" }, TestContext.Current.CancellationToken);
            saved.EnsureSuccessStatusCode();
        }
    }
}
