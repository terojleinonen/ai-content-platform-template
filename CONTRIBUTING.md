# Contributing

Thanks for helping improve the AI Content Platform template! Bug reports, fixes, documentation
and new features are all welcome.

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md); by participating you agree to
uphold it.

## Reporting bugs and ideas

Open an [issue](https://github.com/terojleinonen/ai-content-platform-template/issues) with what
you expected, what happened, and how to reproduce it (steps, request/response, logs). Say which
AI provider you used (Mock, Anthropic, OpenAI) and which database (SQLite, PostgreSQL).

**Security issues:** please don't open a public issue. Report them privately as described in
the [security policy](SECURITY.md).

## Development setup

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and Node.js 22+.
Docker is optional (needed only to run the PostgreSQL stack locally).

```bash
# API on http://localhost:5080 (SQLite, mock AI; no keys needed)
cd backend/src/AiContentPlatform.Api && dotnet run

# Frontend with hot reload on http://localhost:5173 (proxies /api to :5080)
cd frontend && npm install && npm run dev
```

To work against real AI output, add a key with `dotnet user-secrets` (see the README). Never
commit keys, `.env` files or `*.db` files; they are git-ignored.

## Making a change

1. Fork the repository and create a branch from `main` (e.g. `feature/short-description` or
   `fix/short-description`).
2. Keep the change focused: one feature or fix per pull request.
3. Add or update tests for what you change (see below).
4. Update the README when behavior, configuration or the API changes.
5. Open a pull request describing **what** changed, **why**, and **how you tested it**.
   Pull requests are squash-merged once CI passes and the change is reviewed.

### Code style

- Match the surrounding code: naming, structure, comment density.
- The backend builds with nullable reference types and must have **0 warnings**.
- The frontend must pass `npm run build` (TypeScript strict mode, no unused locals).
- Prefer small, readable changes over clever ones; explain *why* in comments where it isn't obvious.

## Tests

```bash
# Backend (SQLite)
cd backend && dotnet test

# Backend against PostgreSQL (each test class gets a throwaway database)
TEST_POSTGRES_CONNECTION="Host=localhost;Username=postgres;Password=postgres" dotnet test

# Frontend typecheck + build
cd frontend && npm run build

# End-to-end check of a running instance (needs curl and jq)
scripts/smoke-test.sh http://localhost:5080
```

Integration tests run the real API in memory with the mock AI providers, so they need no API
keys and cost nothing. Provider code (Anthropic, OpenAI) is tested against recorded streams with
a stub HTTP handler; add a recorded stream when you change how a provider is called or parsed.

## Database changes

The app supports SQLite and PostgreSQL, each with its own migrations. A schema change needs a
migration **for both**:

```bash
cd backend && dotnet tool restore
dotnet ef migrations add <Name> --project src/AiContentPlatform.Api --context SqliteAppDbContext --output-dir Data/Migrations/Sqlite
dotnet ef migrations add <Name> --project src/AiContentPlatform.Api --context PostgresAppDbContext --output-dir Data/Migrations/Postgres
```

Review the generated migrations before committing; existing databases are upgraded in place on
startup, so migrations must keep existing data.

## Continuous integration

Every pull request runs:

| Job | What it checks |
|---|---|
| backend (SQLite) | build, all tests, migrations match the model for both databases |
| backend (PostgreSQL) | all tests against a real PostgreSQL |
| frontend | typecheck and production build |
| docker compose | builds the image, starts the stack with PostgreSQL, runs the smoke test before and after a restart |

All jobs must pass before merging.

## License

By contributing, you agree that your contributions are licensed under the project's
[MIT License](LICENSE).
