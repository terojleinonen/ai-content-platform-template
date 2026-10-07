using System.Text.Json.Serialization;
using AiContentPlatform.Api.Auth;
using AiContentPlatform.Api.Data;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Options;
using AiContentPlatform.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Every controller requires a signed-in user unless marked [AllowAnonymous].
builder.Services.AddControllers(o => o.Filters.Add(new AuthorizeFilter()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddExceptionHandler<AiProviderExceptionHandler>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Persistence (SQLite file, migrated and seeded on startup). The factory also registers a scoped
// AppDbContext; background-safe code (usage recording, parallel variants) creates its own contexts.
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Accounts: ASP.NET Core Identity with cookie sign-in (also bearer tokens for API clients).
builder.Services.AddIdentityApiEndpoints<User>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddUserManager<AppUserManager>();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "aicp.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
    o.SlidingExpiration = true;
    // An API answers with status codes, not redirects to a login page.
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

// Google / Microsoft sign-in, each enabled only when its client id and secret are configured.
var authentication = builder.Services.AddAuthentication();
if (builder.Configuration["Authentication:Google:ClientId"] is { Length: > 0 } googleId)
{
    authentication.AddGoogle(o =>
    {
        o.ClientId = googleId;
        o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
}
if (builder.Configuration["Authentication:Microsoft:ClientId"] is { Length: > 0 } microsoftId)
{
    authentication.AddMicrosoftAccount(o =>
    {
        o.ClientId = microsoftId;
        o.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"]!;
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ExternalLoginService>();

// AI configuration. Standard ANTHROPIC_API_KEY / OPENAI_API_KEY env vars are honoured as fallbacks.
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.PostConfigure<AiOptions>(o =>
{
    if (string.IsNullOrWhiteSpace(o.Anthropic.ApiKey)) o.Anthropic.ApiKey = builder.Configuration["ANTHROPIC_API_KEY"];
    if (string.IsNullOrWhiteSpace(o.OpenAI.ApiKey)) o.OpenAI.ApiKey = builder.Configuration["OPENAI_API_KEY"];
});

// AI providers
static void ConfigureAiClient(HttpClient client, string baseUrl)
{
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    client.Timeout = TimeSpan.FromMinutes(3);
}

builder.Services.AddHttpClient<AnthropicTextProvider>((sp, c) =>
    ConfigureAiClient(c, sp.GetRequiredService<IOptions<AiOptions>>().Value.Anthropic.BaseUrl));
builder.Services.AddHttpClient<OpenAiTextProvider>((sp, c) =>
    ConfigureAiClient(c, sp.GetRequiredService<IOptions<AiOptions>>().Value.OpenAI.BaseUrl));
builder.Services.AddHttpClient<OpenAiImageService>((sp, c) =>
    ConfigureAiClient(c, sp.GetRequiredService<IOptions<AiOptions>>().Value.OpenAI.BaseUrl));
builder.Services.AddSingleton<MockTextProvider>();
builder.Services.AddSingleton<MockImageService>();

builder.Services.AddTransient<ITextGenerationProvider>(sp =>
    AiProviderSelector.ResolveText(sp.GetRequiredService<IOptions<AiOptions>>().Value) switch
    {
        AiProviderNames.Anthropic => sp.GetRequiredService<AnthropicTextProvider>(),
        AiProviderNames.OpenAI => sp.GetRequiredService<OpenAiTextProvider>(),
        _ => sp.GetRequiredService<MockTextProvider>()
    });
builder.Services.AddTransient<IAiImageService>(sp =>
    AiProviderSelector.ResolveImage(sp.GetRequiredService<IOptions<AiOptions>>().Value) switch
    {
        AiProviderNames.OpenAI => sp.GetRequiredService<OpenAiImageService>(),
        _ => sp.GetRequiredService<MockImageService>()
    });

// Application services
builder.Services.AddTransient<IAiTextService, AiTextService>();
builder.Services.AddSingleton<ISeoScoringService, SeoScoringService>();
builder.Services.AddSingleton<IUsageRecorder, UsageRecorder>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var options = scope.ServiceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
    app.Logger.LogInformation("AI providers: text={TextProvider}, image={ImageProvider}",
        AiProviderSelector.ResolveText(options), AiProviderSelector.ResolveImage(options));

    await SeedData.InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS redirection is left to the reverse proxy / hosting platform in this demo.
app.UseCors();

// Serves the built frontend (frontend `npm run build` outputs to wwwroot).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/auth").WithTags("Auth").MapIdentityApi<User>();
app.MapControllers();
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}
