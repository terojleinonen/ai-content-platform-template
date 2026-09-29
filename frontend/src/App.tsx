import { useEffect, useState } from 'react'
import { api } from './api'
import { ImagesPage } from './pages/ImagesPage'
import { ProjectsPage } from './pages/ProjectsPage'
import { WritePage } from './pages/WritePage'
import type { Health } from './types'

const PAGES = {
  write: { label: 'Write', component: WritePage },
  projects: { label: 'Projects', component: ProjectsPage },
  images: { label: 'Images', component: ImagesPage },
} as const
type PageKey = keyof typeof PAGES

const pageFromHash = (): PageKey => {
  const key = window.location.hash.replace('#/', '')
  return key in PAGES ? (key as PageKey) : 'write'
}

export default function App() {
  const [page, setPage] = useState<PageKey>(pageFromHash)
  const [health, setHealth] = useState<Health | null>()

  useEffect(() => {
    const onHash = () => setPage(pageFromHash())
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [])

  useEffect(() => {
    api.health().then(setHealth, () => setHealth(null))
  }, [])

  const Page = PAGES[page].component

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="logo">✍️</span> AI Content Platform
        </div>
        <nav>
          {Object.entries(PAGES).map(([key, { label }]) => (
            <a key={key} href={`#/${key}`} className={key === page ? 'active' : ''}>
              {label}
            </a>
          ))}
        </nav>
        <div className="status">
          {health === null ? (
            <span className="pill bad">API offline</span>
          ) : health ? (
            <span
              className={`pill ${health.textProvider === 'Mock' ? 'warn' : 'good'}`}
              title={
                health.textProvider === 'Mock'
                  ? 'Using built-in demo generators. Set ANTHROPIC_API_KEY or OPENAI_API_KEY for real AI output.'
                  : `Model: ${health.textModel}`
              }
            >
              Text: {health.textProvider} · Images: {health.imageProvider}
            </span>
          ) : null}
        </div>
      </header>

      {health === null && (
        <div className="banner">
          Can’t reach the API. Start the backend with <code>dotnet run</code> in <code>backend/src/AiContentPlatform.Api</code>.
        </div>
      )}

      <main>
        <Page />
      </main>
    </div>
  )
}
