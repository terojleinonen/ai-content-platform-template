import { useEffect, useRef, useState, type FormEvent, type MouseEvent } from 'react'
import { api, parseKeywords } from '../api'
import { Markdown } from '../components/Markdown'
import { SeoPanel } from '../components/SeoPanel'
import { CONTENT_TYPES, CONTENT_TYPE_LABELS, type ContentType, type GenerateContentResponse, type Project } from '../types'

const TONES = ['', 'Friendly', 'Professional', 'Playful', 'Persuasive']

const EXAMPLES = [
  { prompt: 'How small bakeries can use Instagram to attract local customers', type: 'BlogPost', keywords: 'bakery marketing, instagram' },
  { prompt: 'Noise-cancelling wireless earbuds with 30h battery life', type: 'ProductDescription', keywords: 'wireless earbuds, noise cancelling' },
  { prompt: 'Announce our spring sale: 25% off all plants this weekend', type: 'SocialPost', keywords: 'spring sale, plants' },
] as const

type Result = GenerateContentResponse & { stopped?: boolean }

/** Splits streamed Markdown into the "# Title" first line and the body, like the API does. */
function splitTitle(markdown: string) {
  const text = markdown.trimStart()
  if (!text.startsWith('# ')) return { title: '', body: text }
  const newline = text.indexOf('\n')
  return newline < 0
    ? { title: text.slice(2).trim(), body: '' }
    : { title: text.slice(2, newline).trim(), body: text.slice(newline + 1).trim() }
}

export function WritePage() {
  const [type, setType] = useState<ContentType>('BlogPost')
  const [prompt, setPrompt] = useState('')
  const [title, setTitle] = useState('')
  const [audience, setAudience] = useState('')
  const [tone, setTone] = useState('')
  const [language, setLanguage] = useState('English')
  const [keywords, setKeywords] = useState('')

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string>()
  const [result, setResult] = useState<Result>()
  const [streamText, setStreamText] = useState<string>()
  const [editing, setEditing] = useState(false)
  const abortRef = useRef<AbortController | null>(null)

  const [projects, setProjects] = useState<Project[]>([])
  const [projectId, setProjectId] = useState('')
  const [saveState, setSaveState] = useState<string>()

  useEffect(() => {
    api.listProjects().then((p) => {
      setProjects(p)
      setProjectId((current) => current || p[0]?.id || '')
    }, () => {})
  }, [])

  // Cancel an in-flight generation when leaving the page.
  useEffect(() => () => abortRef.current?.abort(), [])

  async function generate(e: FormEvent) {
    e.preventDefault()
    if (abortRef.current) return
    setLoading(true)
    setError(undefined)
    setSaveState(undefined)
    setResult(undefined)
    setEditing(false)
    setStreamText('')

    const controller = new AbortController()
    abortRef.current = controller
    let text = ''
    try {
      const res = await api.generateContentStream(
        {
          prompt,
          type,
          title: title || undefined,
          targetAudience: audience || undefined,
          toneOfVoice: tone || undefined,
          language: language || undefined,
          keywords: parseKeywords(keywords),
        },
        (chunk) => {
          text += chunk
          setStreamText(text)
        },
        controller.signal,
      )
      setResult(res)
    } catch (err) {
      if (controller.signal.aborted && text.trim()) {
        // Keep what was written so far; it can still be edited and saved.
        const partial = splitTitle(text)
        setResult({
          title: partial.title || title || 'Untitled',
          body: partial.body,
          wordCount: partial.body.split(/\s+/).filter(Boolean).length,
          provider: '',
          stopped: true,
        })
      } else if (!controller.signal.aborted) {
        setError((err as Error).message)
      }
    } finally {
      abortRef.current = null
      setStreamText(undefined)
      setLoading(false)
    }
  }

  const stop = (e: MouseEvent) => {
    // Aborting re-renders this spot as the "Generate" submit button before the click finishes;
    // without preventDefault the same click would submit the form and start a new generation.
    e.preventDefault()
    abortRef.current?.abort()
  }
  const live = streamText !== undefined ? splitTitle(streamText) : undefined

  async function save() {
    if (!result || !projectId) return
    setSaveState('Saving…')
    try {
      await api.addContent(projectId, {
        type,
        title: result.title,
        body: result.body,
        targetAudience: audience || undefined,
        toneOfVoice: tone || undefined,
        keywords: parseKeywords(keywords),
      })
      const name = projects.find((p) => p.id === projectId)?.name
      setSaveState(`Saved to “${name}”`)
    } catch (err) {
      setSaveState(`Save failed: ${(err as Error).message}`)
    }
  }

  function applyExample(i: number) {
    const ex = EXAMPLES[i]
    setPrompt(ex.prompt)
    setType(ex.type)
    setKeywords(ex.keywords)
  }

  return (
    <div className="split">
      <form className="card form" onSubmit={generate}>
        <h2>Content brief</h2>

        <label>
          Content type
          <select value={type} onChange={(e) => setType(e.target.value as ContentType)}>
            {CONTENT_TYPES.map((t) => (
              <option key={t} value={t}>
                {CONTENT_TYPE_LABELS[t]}
              </option>
            ))}
          </select>
        </label>

        <label>
          What should we write about? *
          <textarea
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            rows={4}
            placeholder="e.g. Tips for first-time home buyers in 2026"
            required
          />
        </label>
        <div className="examples">
          <span className="muted small">Try:</span>
          {EXAMPLES.map((ex, i) => (
            <button type="button" key={i} className="chip" onClick={() => applyExample(i)}>
              {CONTENT_TYPE_LABELS[ex.type]}
            </button>
          ))}
        </div>

        <label>
          <span>
            Title <span className="muted">(optional)</span>
          </span>
          <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Let the AI decide" />
        </label>

        <div className="row">
          <label>
            Target audience
            <input value={audience} onChange={(e) => setAudience(e.target.value)} placeholder="e.g. small business owners" />
          </label>
          <label>
            Tone of voice
            <select value={tone} onChange={(e) => setTone(e.target.value)}>
              {TONES.map((t) => (
                <option key={t} value={t}>
                  {t || 'Neutral'}
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="row">
          <label>
            Language
            <input value={language} onChange={(e) => setLanguage(e.target.value)} />
          </label>
          <label>
            <span>
              SEO keywords <span className="muted">(comma-separated)</span>
            </span>
            <input value={keywords} onChange={(e) => setKeywords(e.target.value)} placeholder="keyword one, keyword two" />
          </label>
        </div>

        {loading ? (
          <button key="stop" type="button" className="primary stop" onClick={stop}>
            <span className="spinner" /> Stop generating
          </button>
        ) : (
          <button key="generate" className="primary" disabled={!prompt.trim()}>
            ✨ Generate content
          </button>
        )}
        {error && <p className="error">{error}</p>}
      </form>

      <section className="card result">
        {live ? (
          <>
            <div className="result-toolbar">
              <span className="pill neutral">Writing…</span>
              <div className="spacer" />
              <button type="button" className="ghost" onClick={stop}>
                ■ Stop
              </button>
            </div>
            <article className="streaming">
              {live.title && <h1 className="result-title">{live.title}</h1>}
              {live.body ? <Markdown text={live.body} /> : <span className="cursor" />}
            </article>
          </>
        ) : !result ? (
          <div className="empty">
            <div className="empty-icon">✍️</div>
            <p>Fill in the brief and hit <strong>Generate</strong>.</p>
            <p className="muted small">Generated content appears here with SEO insights. You can edit it and save it to a project.</p>
          </div>
        ) : (
          <>
            <div className="result-toolbar">
              {result.stopped ? (
                <span className="pill warn">Stopped early</span>
              ) : (
                <span className="pill neutral">via {result.provider}</span>
              )}
              <div className="spacer" />
              <button type="button" className="ghost" onClick={() => setEditing(!editing)}>
                {editing ? 'Preview' : 'Edit'}
              </button>
              <button type="button" className="ghost" onClick={() => navigator.clipboard.writeText(`# ${result.title}\n\n${result.body}`)}>
                Copy
              </button>
            </div>

            {editing ? (
              <div className="form">
                <input className="title-input" value={result.title} onChange={(e) => setResult({ ...result, title: e.target.value })} />
                <textarea rows={18} value={result.body} onChange={(e) => setResult({ ...result, body: e.target.value })} />
              </div>
            ) : (
              <article>
                <h1 className="result-title">{result.title}</h1>
                <Markdown text={result.body} />
              </article>
            )}

            {result.stopped ? (
              <p className="muted small">Generation was stopped, so SEO analysis isn’t available. You can still edit and save the text.</p>
            ) : (
              <SeoPanel wordCount={result.wordCount} scores={result.keywordScores} />
            )}

            <div className="save-bar">
              <select value={projectId} onChange={(e) => setProjectId(e.target.value)} disabled={!projects.length}>
                {projects.length === 0 && <option value="">No projects yet</option>}
                {projects.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
              <button type="button" className="primary" onClick={save} disabled={!projectId}>
                Save to project
              </button>
            </div>
            {saveState && <p className="muted small">{saveState}</p>}
          </>
        )}
      </section>
    </div>
  )
}
