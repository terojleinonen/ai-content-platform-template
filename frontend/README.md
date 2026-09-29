# Frontend (React + TypeScript + Vite)

```bash
npm install
npm run dev      # http://localhost:5173, proxies /api to http://localhost:5080
npm run build    # typecheck + production build into ../backend/src/AiContentPlatform.Api/wwwroot
```

Environment variables:

- `VITE_API_URL` – backend URL used by the dev-server proxy (default `http://localhost:5080`).
- `VITE_API_BASE` – prefix for API calls from the browser (default: same origin).

Pages: **Write** (generate + SEO + save), **Projects** (library/editor), **Images** (image generation).
