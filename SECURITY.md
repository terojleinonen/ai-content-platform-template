# Security policy

## Supported versions

This is a template repository without versioned releases. Security fixes are made on the
`main` branch only. If you created a project from the template, compare your copy with `main`
and apply relevant fixes yourself; copies don't receive updates automatically.

## Reporting a vulnerability

**Please don't report security issues in public issues, discussions or pull requests.**

Report them privately through GitHub:
**[Security → Report a vulnerability](https://github.com/terojleinonen/ai-content-platform-template/security/advisories/new)**.
Only the maintainer can see the report.

Please include:

- what the issue is and what an attacker could do with it
- steps to reproduce (requests, configuration, AI provider, SQLite or PostgreSQL)
- the commit you tested and how you ran the app (`dotnet run` or Docker compose)
- a suggested fix, if you have one

The maintainer will acknowledge the report, investigate, and keep you updated in the private
advisory. Once a fix is on `main`, the advisory can be published, with credit to you if you'd
like.

## Scope

In scope: the code in this repository, for example:

- authentication and sessions (sign-in, cookies, Google/Microsoft sign-in, redirects)
- access to another user's projects, content, brand voice or usage data
- injection or cross-site scripting, including through AI-generated content
- secrets exposed by the app (API keys, connection strings) or by the Docker setup

Out of scope:

- vulnerabilities in dependencies (.NET, npm or NuGet packages, the Anthropic or OpenAI APIs);
  please report those to the upstream project, though a heads-up here is welcome if this
  template is affected
- deployments that don't follow the hardening notes below
- the built-in mock AI provider producing odd text

## Deployment hardening

If you run a project built from this template, see the README for details:

- **Secrets:** keep API keys and OAuth secrets in user secrets or environment variables,
  never in the repository or `appsettings.json`.
- **HTTPS:** run behind an HTTPS reverse proxy and set `BEHIND_PROXY=true`, so cookies and
  OAuth redirects use the original scheme.
- **Database:** set a strong `POSTGRES_PASSWORD` and don't expose PostgreSQL publicly.
- **Sign-in keys:** keep the data protection keys volume (`/data/keys`) private and persistent;
  anyone with these keys can forge sign-in cookies.
- **First account:** create your own account right after deploying; the first account takes
  over the seeded demo data.
- **Email:** no email sender is configured, so password reset and email confirmation don't
  send mail until you add one.
