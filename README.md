# AI Content Creation Platform – Demo / Template

A full-stack, working demo of an **AI-powered content creation SaaS**:
ASP.NET Core (.NET 10) API + React/Vite frontend, with pluggable AI providers.

It runs **out of the box with no API keys** (built-in mock generators), and switches to
real AI output as soon as you provide an Anthropic or OpenAI key.

## Features

- **Write** – generate blog posts, product descriptions, social posts and emails from a brief
  (audience, tone, language, SEO keywords). Text streams in live as the model writes it, and can be
  stopped at any time; edit the result and save it to a project.
- **Variants** – write 3 versions of the same brief in parallel, each from a different angle, and pick one.
- **AI editing tools** – Improve, Shorten, Expand, Change tone, Translate, or a custom instruction
  ("add a call to action"), on fresh results (with undo) and on saved content (review, then save or cancel).
- **SEO insights** – word count and keyword density per keyword (multi-word phrases supported),
  rated *missing / low / good / too high*.
- **Projects** – organize saved content; view, edit and delete items (SQLite via EF Core).
- **Images** – generate images from a prompt with style and format options.
- **Pluggable AI providers**

  | Capability | Mock (default) | Anthropic (Claude) | OpenAI |
  |---|---|---|---|
  | Text  | ✅ template-based, offline | ✅ Messages API | ✅ Chat Completions |
  | Image | ✅ SVG placeholder, offline | – | ✅ Images API (`gpt-image-1`) |

## Quick start

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Node.js 22+.

**Option A – single process** (backend serves the built frontend):

```bash
cd frontend && npm install && npm run build   # outputs to the API's wwwroot
cd ../backend/src/AiContentPlatform.Api && dotnet run
```

Open http://localhost:5080.

**Option B – development with hot reload** (two terminals):

```bash
# Terminal 1 – API on http://localhost:5080 (Swagger UI at /swagger)
cd backend/src/AiContentPlatform.Api
dotnet run

# Terminal 2 – Vite dev server on http://localhost:5173 (proxies /api to :5080)
cd frontend
npm install
npm run dev
```

On first start the API creates `ai-content-platform.db` (SQLite) and seeds a demo project.
Delete the file to reset the demo data.

## Using a real AI provider

With `Ai:TextProvider` / `Ai:ImageProvider` set to `Auto` (the default), the API picks the first
provider that has a key: **Anthropic → OpenAI → Mock** for text, **OpenAI → Mock** for images.
The active providers are shown in the top-right badge and at `GET /api/health`.

```bash
# Environment variables…
export ANTHROPIC_API_KEY=sk-ant-...
export OPENAI_API_KEY=sk-...

# …or user secrets (kept outside the repo)
cd backend/src/AiContentPlatform.Api
dotnet user-secrets set "Ai:Anthropic:ApiKey" "sk-ant-..."
dotnet user-secrets set "Ai:OpenAI:ApiKey" "sk-..."
```

Other settings (in `appsettings.json`, overridable via env vars like `Ai__Anthropic__Model`):

| Setting | Default |
|---|---|
| `Ai:TextProvider` | `Auto` (`Mock`, `Anthropic`, `OpenAI`) |
| `Ai:ImageProvider` | `Auto` (`Mock`, `OpenAI`) |
| `Ai:Anthropic:Model` | `claude-sonnet-5-5` |
| `Ai:Mock:StreamDelayMs` | `25` (simulated typing speed of the mock) |
| `Ai:Anthropic:MaxTokens` | `2048` |
| `Ai:OpenAI:Model` / `ImageModel` | `gpt-4.1-mini` / `gpt-image-1` |
| `Ai:OpenAI:BaseUrl` | `https://api.openai.com/` (point at any OpenAI-compatible endpoint) |
| `ConnectionStrings:Default` | `Data Source=ai-content-platform.db` |

Provider errors are returned as HTTP 502 `ProblemDetails` and shown in the UI.

## API

| Method | Route | Description |
|---|---|---|
| GET | `/api/health` | Status and active AI providers |
| POST | `/api/content/generate` | Generate content + SEO scores |
| POST | `/api/content/generate/stream` | Same, streamed as Server-Sent Events (see below) |
| POST | `/api/content/variants?count=3` | Generate 2–4 alternative versions in parallel |
| POST | `/api/content/transform` | Rewrite content: `Improve`, `Shorten`, `Expand`, `ChangeTone`, `Translate`, `Custom` |
| POST | `/api/content/transform/stream` | Same, streamed as Server-Sent Events |
| POST | `/api/image/generate` | Generate an image (URL or `data:` URI) |
| GET/POST | `/api/projects` | List / create projects |
| GET/PUT/DELETE | `/api/projects/{id}` | Get / rename / delete a project |
| GET/POST | `/api/projects/{id}/content` | List / add content items |
| GET/PUT/DELETE | `/api/content-items/{id}` | Get / update / delete a content item |

The streaming endpoints send `delta` events with `{"text": "..."}` Markdown chunks as they are
written, then one `done` event with the full response (title, body, SEO). If the provider fails
mid-stream it sends an `error` event with `{"message": "..."}`. Closing the connection cancels the
upstream AI request.

```bash
curl -N -X POST localhost:5080/api/content/generate/stream \
  -H 'content-type: application/json' -d '{"prompt":"Coffee brewing tips","type":"SocialPost"}'
```

Explore and try all endpoints with Swagger UI at http://localhost:5080/swagger (Development).

## Project structure

```text
.
├── backend
│   ├── AiContentPlatform.slnx
│   ├── src/AiContentPlatform.Api
│   │   ├── Controllers      # Content, Image, Projects, ContentItems, Health
│   │   ├── Data             # EF Core DbContext + demo seed data
│   │   ├── Domain           # User, Project, ContentItem
│   │   ├── Dtos
│   │   ├── Options          # AI provider configuration
│   │   └── Services         # AI orchestration, providers, SEO scoring
│   └── tests/AiContentPlatform.Api.Tests   # xUnit unit + integration tests
├── frontend                 # React 19 + TypeScript + Vite
│   └── src
│       ├── pages            # Write, Projects, Images
│       └── components       # Markdown renderer, SEO panel
└── .github/workflows/ci.yml # Build + test backend, build frontend
```

### Adding another AI provider

For an LLM text provider, derive from `LlmTextProvider` and implement `StreamCompletionAsync`
(system + user prompt in, streamed text out); prompts for generation and editing come from
`ContentPrompt`. For images, implement `IAiImageService`. Then register it in `Program.cs` and add
it to `AiProviderSelector`.

## Tests

```bash
cd backend && dotnet test
```

Integration tests run the real API in-memory (`WebApplicationFactory`) against a temporary
SQLite database with the mock providers; the Anthropic client is tested against a stub HTTP handler.

## Next steps / TODO (for a production SaaS)

- Authentication & authorization (ASP.NET Core Identity / JWT / OIDC); projects are currently
  owned by a seeded demo user.
- EF Core migrations and PostgreSQL instead of `EnsureCreated` + SQLite.
- Streaming generation, rate limiting, usage metering and billing.
- Persist generated images to blob storage instead of returning `data:` URIs.
- Docker images and deployment pipeline.

---

This repo is designed as a **GitHub Template Repository** – push it to GitHub and enable
"Use this template".
