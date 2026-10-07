using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Auth;
using AiContentPlatform.Api.Controllers;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AiContentPlatform.Api.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public AuthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Anonymous() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    [Theory]
    [InlineData("GET", "/api/projects")]
    [InlineData("GET", "/api/usage")]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/content/generate")]
    [InlineData("POST", "/api/content/generate/stream")]
    [InlineData("POST", "/api/image/generate")]
    public async Task ApiEndpoints_RequireSignIn(string method, string path)
    {
        var response = await Anonymous().SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST" ? JsonContent.Create(new { prompt = "x" }) : null
        }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/auth/providers")]
    public async Task PublicEndpoints_AreOpen(string path)
    {
        var response = await Anonymous().GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_Login_Me_Logout()
    {
        var client = await _factory.CreateUserClientAsync("jane.doe@test.local");

        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/auth/me", Ct);
        Assert.Equal("jane.doe@test.local", me!.Email);
        Assert.Equal("jane.doe", me.DisplayName);
        Assert.True(me.HasPassword);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/register", new { email = "weak@test.local", password = "short" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("PasswordTooShort", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await _factory.CreateUserClientAsync("wrong-pw@test.local");

        var response = await Anonymous().PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email = "wrong-pw@test.local", password = "Not-the-password-1" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_CannotSeeOrChangeEachOthersData()
    {
        var alice = await _factory.CreateUserClientAsync();
        var bob = await _factory.CreateUserClientAsync();

        var project = await (await alice.PostAsJsonAsync("/api/projects", new SaveProjectRequest { Name = "Alice's project" }, Json, Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);
        var item = await (await alice.PostAsJsonAsync($"/api/projects/{project!.Id}/content",
            new SaveContentItemRequest { Title = "Secret", Body = "Alice only" }, Json, Ct))
            .Content.ReadFromJsonAsync<ContentItemDto>(Json, Ct);
        await alice.PostAsJsonAsync("/api/content/generate", new GenerateContentRequest { Prompt = "alice", ProjectId = project.Id }, Json, Ct);

        var bobsProjects = await bob.GetFromJsonAsync<List<ProjectDto>>("/api/projects", Json, Ct);
        Assert.DoesNotContain(bobsProjects!, p => p.Id == project.Id);

        // Every way of reaching Alice's data looks like it doesn't exist.
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/projects/{project.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/projects/{project.Id}/content", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/projects/{project.Id}", new SaveProjectRequest { Name = "x" }, Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/projects/{project.Id}/brand-voice", new SaveBrandVoiceRequest { Voice = "x" }, Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/projects/{project.Id}/content", new SaveContentItemRequest { Title = "x", Body = "x" }, Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/projects/{project.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/content-items/{item!.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/content-items/{item.Id}", new SaveContentItemRequest { Title = "x", Body = "x" }, Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/content-items/{item.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/content/generate", new GenerateContentRequest { Prompt = "x", ProjectId = project.Id }, Json, Ct)).StatusCode);

        var bobsUsage = await bob.GetFromJsonAsync<UsageReport>("/api/usage", Json, Ct);
        Assert.Equal(0, bobsUsage!.Totals.Calls);
        var alicesUsage = await alice.GetFromJsonAsync<UsageReport>("/api/usage", Json, Ct);
        Assert.Contains(alicesUsage!.ByProject, g => g.Label == "Alice's project");

        // Alice's data is untouched.
        Assert.Equal("Secret", (await alice.GetFromJsonAsync<ContentItemDto>($"/api/content-items/{item.Id}", Json, Ct))!.Title);
    }

    [Fact]
    public async Task OnlyTheFirstAccount_TakesOverTheDemoProject()
    {
        await using var factory = new ApiFactory();

        var first = await factory.CreateUserClientAsync();
        var second = await factory.CreateUserClientAsync();

        Assert.Contains(await first.GetFromJsonAsync<List<ProjectDto>>("/api/projects", Json, Ct) ?? [], p => p.Name == "Acme Coffee Launch");
        Assert.Empty(await second.GetFromJsonAsync<List<ProjectDto>>("/api/projects", Json, Ct) ?? []);
    }

    [Fact]
    public async Task ExternalLogin_CreatesAccountThenFindsItAgain()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ExternalLoginService>();

        var created = await service.ResolveUserAsync(GoogleLogin("google-123", "new.person@test.local", "New Person"));
        Assert.NotNull(created.User);
        Assert.Equal("New Person", created.User.DisplayName);
        Assert.True(created.User.EmailConfirmed);

        var again = await service.ResolveUserAsync(GoogleLogin("google-123", "new.person@test.local", "New Person"));
        Assert.Equal(created.User.Id, again.User!.Id);
    }

    [Fact]
    public async Task ExternalLogin_DoesNotLinkToExistingPasswordAccountByEmail()
    {
        await _factory.CreateUserClientAsync("taken@test.local");
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ExternalLoginService>();

        var result = await service.ResolveUserAsync(GoogleLogin("google-999", "taken@test.local", "Imposter"));

        Assert.Null(result.User);
        Assert.Contains("already exists", result.Error);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        Assert.Empty(await users.GetLoginsAsync((await users.FindByEmailAsync("taken@test.local"))!));
    }

    [Fact]
    public async Task ExternalLogin_WithoutEmail_IsRefused()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ExternalLoginService>();

        var result = await service.ResolveUserAsync(GoogleLogin("google-555", email: null, "No Email"));

        Assert.Null(result.User);
        Assert.Contains("didn't share an email", result.Error);
    }

    [Fact]
    public async Task ConfiguredGoogle_IsListedAndRedirectsToGoogle()
    {
        await using var factory = new ApiFactory();
        var client = factory
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Authentication:Google:ClientId", "test-client-id");
                b.UseSetting("Authentication:Google:ClientSecret", "test-secret");
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(["Google"], await client.GetFromJsonAsync<List<string>>("/api/auth/providers", Ct));

        var challenge = await client.GetAsync("/api/auth/external/google?returnUrl=/", Ct);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var location = challenge.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", location);
        Assert.Contains("client_id=test-client-id", location);
        Assert.Contains(Uri.EscapeDataString("/signin-google"), location);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/auth/external/microsoft", Ct)).StatusCode);
    }

    [Fact]
    public async Task ExternalCallback_NeverRedirectsOffSite()
    {
        var response = await Anonymous().GetAsync("/api/auth/external/callback?returnUrl=https://evil.example/steal", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/?authError=", response.Headers.Location!.ToString());
    }

    private static ExternalLoginInfo GoogleLogin(string key, string? email, string name)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, key), new(ClaimTypes.Name, name) };
        if (email is not null) claims.Add(new Claim(ClaimTypes.Email, email));
        return new ExternalLoginInfo(new ClaimsPrincipal(new ClaimsIdentity(claims, "Google")), "Google", key, "Google");
    }
}
