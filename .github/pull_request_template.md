## What and why

<!-- What does this change, and why is it needed? Link the issue if there is one (e.g. "Fixes #12"). -->

## How it was tested

<!-- Commands you ran, and any manual checks (mock provider, real AI, PostgreSQL, Docker). -->

## Checklist

- [ ] One focused change
- [ ] Tests added or updated (`cd backend && dotnet test`)
- [ ] Backend builds with 0 warnings; `cd frontend && npm run build` passes
- [ ] Schema change? A migration for **both** SQLite and PostgreSQL (see CONTRIBUTING.md)
- [ ] README updated if behavior, configuration or the API changed
- [ ] CHANGELOG.md `Unreleased` entry for user-visible changes
- [ ] No secrets, `.env` or database files committed
