# Frontend Placeholder

This folder is reserved for the web frontend (Blazor WebAssembly, React, or another SPA framework).

## Suggested setup (option A – Blazor WebAssembly)

```bash
dotnet new blazorwasm -n AiContentPlatform.Frontend
```

Then move the generated project into this folder and configure it to call the backend API at `/api/...`.

## Suggested setup (option B – React + Vite)

```bash
npm create vite@latest ai-content-platform-frontend -- --template react-ts
```

Then move the created project into this folder, configure environment variables for the backend API URL, and implement pages:

- Content editor
- Templates gallery
- Media library
- Settings / billing
