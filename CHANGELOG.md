# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-10-08

First release: the starter template turned into a complete, working AI content platform.

### Added

- **Working demo**: ASP.NET Core API and React + TypeScript + Vite frontend (Write, Projects,
  Images), SQLite persistence, xUnit tests and CI. Runs without API keys using a built-in mock
  AI provider; Anthropic (Claude) and OpenAI are used when keys are configured.
  ([#1](https://github.com/terojleinonen/ai-content-platform-template/pull/1))
- **Live streaming**: generated text appears as it is written (Server-Sent Events), with a Stop
  button that cancels the AI request. ([#2](https://github.com/terojleinonen/ai-content-platform-template/pull/2))
- **AI editing tools**: Improve, Shorten, Expand, Change tone, Translate and custom
  instructions, with undo; plus generating three variants of a brief to compare.
  ([#3](https://github.com/terojleinonen/ai-content-platform-template/pull/3))
- **Brand voice per project**: voice, audience, key facts, preferred and banned terms, applied to
  every prompt, with a brand check on the output. EF Core migrations replace `EnsureCreated`,
  upgrading existing databases in place. ([#4](https://github.com/terojleinonen/ai-content-platform-template/pull/4))
- **Finnish-aware SEO and brand checks**: inflected forms count (case endings and consonant
  gradation, plus English plurals), and keywords and brand terms are translated into the
  content's language before writing. ([#5](https://github.com/terojleinonen/ai-content-platform-template/pull/5))
- **SEO by content type**: short texts are rated by mentions; product descriptions and longer
  texts by density ranges that fit their length. ([#6](https://github.com/terojleinonen/ai-content-platform-template/pull/6))
- **Usage and cost tracking**: every AI call is recorded with tokens, estimated cost, duration
  and outcome; a Usage page with totals, a daily chart and breakdowns.
  ([#7](https://github.com/terojleinonen/ai-content-platform-template/pull/7))
- **User accounts**: email and password sign-in, optional Google and Microsoft sign-in, and
  per-user data. The first account takes over existing demo data.
  ([#8](https://github.com/terojleinonen/ai-content-platform-template/pull/8))
- **PostgreSQL and Docker**: choose SQLite or PostgreSQL, a multi-stage Docker image and a
  compose stack, a smoke-test script, and CI that tests on both databases and runs the stack.
  ([#9](https://github.com/terojleinonen/ai-content-platform-template/pull/9))
- **Refusal handling and fallback**: Claude declines are reported clearly, and server-side
  fallback re-serves them on Anthropic's recommended model.
  ([#10](https://github.com/terojleinonen/ai-content-platform-template/pull/10))
- **Effort setting**: `Ai:Anthropic:Effort` controls how much Claude thinks.
  ([#11](https://github.com/terojleinonen/ai-content-platform-template/pull/11))
- **Project documentation and community files**: getting started from the template, MIT
  license, contributing guide, code of conduct, issue forms and templates, pull request
  template, security policy and this changelog.
  ([#12](https://github.com/terojleinonen/ai-content-platform-template/pull/12),
  [#13](https://github.com/terojleinonen/ai-content-platform-template/pull/13),
  [#14](https://github.com/terojleinonen/ai-content-platform-template/pull/14),
  [#15](https://github.com/terojleinonen/ai-content-platform-template/pull/15),
  [#16](https://github.com/terojleinonen/ai-content-platform-template/pull/16),
  [#17](https://github.com/terojleinonen/ai-content-platform-template/pull/17),
  [#18](https://github.com/terojleinonen/ai-content-platform-template/pull/18))

### Changed

- Upgraded from .NET 8 to .NET 10 (LTS). ([#1](https://github.com/terojleinonen/ai-content-platform-template/pull/1))
- Claude is called through the official Anthropic C# SDK instead of raw HTTP, with automatic
  retries for rate limits and server errors. ([#10](https://github.com/terojleinonen/ai-content-platform-template/pull/10))
- The default model is Claude Opus 5.5 (`claude-opus-5-5`) with `medium` effort, and the token
  limit is raised to 16,000 to leave room for thinking. ([#11](https://github.com/terojleinonen/ai-content-platform-template/pull/11))

### Fixed

- The original template didn't compile (a malformed raw string literal).
  ([#1](https://github.com/terojleinonen/ai-content-platform-template/pull/1))
- SEO scoring never matched multi-word keywords. ([#1](https://github.com/terojleinonen/ai-content-platform-template/pull/1))
- Generated images pointed at a placeholder service that no longer exists; the mock now draws
  them locally. ([#1](https://github.com/terojleinonen/ai-content-platform-template/pull/1))
- Cancelling an edit of saved content kept the unsaved changes.
  ([#3](https://github.com/terojleinonen/ai-content-platform-template/pull/3))
- Responses cut off at the token limit were shown as complete; they are now reported as errors.
  ([#11](https://github.com/terojleinonen/ai-content-platform-template/pull/11))

[Unreleased]: https://github.com/terojleinonen/ai-content-platform-template/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/terojleinonen/ai-content-platform-template/releases/tag/v1.0.0
