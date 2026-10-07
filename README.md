# AI Content Creation Platform – Demo / Template

A full-stack, working demo of an **AI-powered content creation SaaS**:
ASP.NET Core (.NET 10) API + React/Vite frontend, with pluggable AI providers.

It runs **out of the box with no API keys** (built-in mock generators), and switches to
real AI output as soon as you provide an Anthropic or OpenAI key.

## Features

- **Accounts** – email + password sign-in (ASP.NET Core Identity, secure HTTP-only cookie), plus
  Google and Microsoft sign-in when configured. Each user sees only their own projects, content and usage.

- **Write** – generate blog posts, product descriptions, social posts and emails from a brief
  (audience, tone, language, SEO keywords). Text streams in live as the model writes it, and can be
  stopped at any time; edit the result and save it to a project.
- **Variants** – write 3 versions of the same brief in parallel, each from a different angle, and pick one.
- **AI editing tools** – Improve, Shorten, Expand, Change tone, Translate, or a custom instruction
  ("add a call to action"), on fresh results (with undo) and on saved content (review, then save or cancel).
- **SEO insights** – keyword usage rated for the content's type and length: short text (social
  posts, under 80 words) by mentions (1–2 is good), product descriptions and medium text by
  density 0.5–5%, long text by density 0.5–3%. Multi-word phrases supported. Inflected forms count: Finnish case endings and consonant
  gradation (*Helsinki → Helsingissä*, *kauppa → kaupassa*) and English plurals.
- **Multilingual keywords** – when content is written in, translated to, or edited in another
  language, keywords and brand terms are first translated to match (one small AI call, cached).
  The AI then writes with exactly those terms and SEO and brand checks score them; banned terms
  stay banned in both languages.
- **Brand voice per project** – voice, default audience, key facts, preferred and banned terms. They're
  added to every prompt for that project (generation, variants, AI tools), and a deterministic
  **brand check** flags banned terms in the output and shows which preferred terms were used.
- **Projects** – organize saved content; view, edit and delete items (SQLite via EF Core).
- **Images** – generate images from a prompt with style and format options.
- **Usage & cost** – every AI call (generation, variants, edits, term translations, images) is
  recorded with tokens, estimated cost, duration and outcome, including calls stopped mid-stream.
  The Usage page shows totals, a daily chart and breakdowns by operation, project and model; each
  result also shows its own tokens and cost.
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

On first start the API creates `ai-content-platform.db` (SQLite), applies EF Core migrations and
seeds a demo project with a brand voice. Delete the file to reset the demo data.

Schema changes are EF Core migrations in `Data/Migrations`, applied automatically on startup:

```bash
cd backend && dotnet tool restore
dotnet ef migrations add <Name> --project src/AiContentPlatform.Api --output-dir Data/Migrations
```

Databases created by earlier versions of this template (before migrations) are upgraded in place.

## Accounts and sign-in

Create an account on the sign-in page. **The first account created takes over the demo
project** (and, when upgrading an existing install, everything created before accounts existed).

Passwords need at least 8 characters with upper- and lowercase letters, a number and a symbol;
5 failed attempts lock the account for 5 minutes.

### Google and Microsoft sign-in (optional)

Each provider is enabled when its client ID and secret are configured (keep secrets out of the repo):

```bash
cd backend/src/AiContentPlatform.Api
dotnet user-secrets set "Authentication:Google:ClientId" "<id>.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>"
dotnet user-secrets set "Authentication:Microsoft:ClientId" "<application id>"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "<secret>"
```

Register these redirect URIs with the provider (use your public URL in production):

| Provider | Where | Redirect URI |
|---|---|---|
| Google | Google Cloud Console → APIs & Services → Credentials → OAuth client (Web) | `http://localhost:5080/signin-google` |
| Microsoft | Azure portal → App registrations → Authentication → Web | `http://localhost:5080/signin-microsoft` |

Notes:
- External sign-in creates an account from the provider's email. If a password account already
  uses that email, the user is asked to sign in with the password instead — accounts are never
  linked on email alone, since not every provider guarantees a verified address.
- Test external sign-in on `http://localhost:5080` (the built frontend), not the Vite dev server.
- Identity's password-reset and email-confirmation endpoints exist, but no email sender is
  configured; plug in an `IEmailSender<User>` before relying on them.

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
| `Ai:Pricing:<model>` | USD per million input/output tokens, used for cost estimates (Claude models preconfigured) |
| `ConnectionStrings:Default` | `Data Source=ai-content-platform.db` |

Provider errors are returned as HTTP 502 `ProblemDetails` and shown in the UI.

## API

All endpoints except `/api/health` and the sign-in endpoints require a signed-in user; data is
scoped to that user (other users' data returns 404).

| Method | Route | Description |
|---|---|---|
| GET | `/api/health` | Status and active AI providers |
| POST | `/api/auth/register`, `/api/auth/login?useCookies=true` | Create an account / sign in (ASP.NET Core Identity; without `useCookies` login returns a bearer token) |
| GET / POST | `/api/auth/me`, `/api/auth/logout` | Current user / sign out |
| GET | `/api/auth/providers`, `/api/auth/external/{provider}` | Configured external providers / start Google or Microsoft sign-in |
| POST | `/api/content/generate` | Generate content + SEO scores (pass `projectId` to use its brand voice) |
| POST | `/api/content/generate/stream` | Same, streamed as Server-Sent Events (see below) |
| POST | `/api/content/variants?count=3` | Generate 2–4 alternative versions in parallel |
| POST | `/api/content/transform` | Rewrite content: `Improve`, `Shorten`, `Expand`, `ChangeTone`, `Translate`, `Custom` |
| POST | `/api/content/transform/stream` | Same, streamed as Server-Sent Events |
| POST | `/api/image/generate` | Generate an image (URL or `data:` URI) |
| GET/POST | `/api/projects` | List / create projects |
| GET/PUT/DELETE | `/api/projects/{id}` | Get / rename / delete a project |
| PUT | `/api/projects/{id}/brand-voice` | Set the project's brand voice (all-empty fields remove it) |
| GET | `/api/usage?days=30` | AI usage and estimated cost: totals, per day, per operation/project/model, recent calls |
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

- Email sending (confirmation, password reset), roles/teams and shared projects.
- PostgreSQL instead of SQLite (migrations are already in place).
- Rate limiting and billing on top of the usage records.
- Persist generated images to blob storage instead of returning `data:` URIs.
- Docker images and deployment pipeline.

---

This repo is designed as a **GitHub Template Repository** – push it to GitHub and enable
"Use this template".
