import { useEffect, useRef, useState, type FormEvent, type MouseEvent } from 'react'
import { api, parseKeywords, translateKeywordField } from '../api'
import { AiTools, TRANSFORM_LABELS, type TransformOptions } from '../components/AiTools'
import { BrandCheckPanel } from '../components/BrandCheckPanel'
import { Markdown } from '../components/Markdown'
import { SeoPanel } from '../components/SeoPanel'
import { useContentStream } from '../hooks/useContentStream'
import {
  CONTENT_TYPES,
  CONTENT_TYPE_LABELS,
  TONES,
  type ContentType,
  type GenerateContentRequest,
  type GenerateContentResponse,
  type Project,
  type TransformAction,
} from '../types'

const EXAMPLES = [
  { prompt: 'How small bakeries can use Instagram to attract local customers', type: 'BlogPost', keywords: 'bakery marketing, instagram' },
  { prompt: 'Noise-cancelling wireless earbuds with 30h battery life', type: 'ProductDescription', keywords: 'wireless earbuds, noise cancelling' },
  { prompt: 'Announce our spring sale: 25% off all plants this weekend', type: 'SocialPost', keywords: 'spring sale, plants' },
] as const

const VARIANT_COUNT = 3

type Result = GenerateContentResponse & { stopped?: boolean }

const snippet = (body: string) => body.replace(/[#*_]/g, '').replace(/\s+/g, ' ').trim().slice(0, 220)

export function WritePage() {
  const [type, setType] = useState<ContentType>('BlogPost')
  const [prompt, setPrompt] = useState('')
  const [title, setTitle] = useState('')
  const [audience, setAudience] = useState('')
  const [tone, setTone] = useState('')
  const [language, setLanguage] = useState('English')
  const [keywords, setKeywords] = useState('')

  const stream = useContentStream()
  const [streamLabel, setStreamLabel] = useState('Writing…')
  const [error, setError] = useState<string>()
  const [notice, setNotice] = useState<string>()
  const [result, setResult] = useState<Result>()
  const [history, setHistory] = useState<Result[]>([])
  const [editing, setEditing] = useState(false)

  const [variants, setVariants] = useState<GenerateContentResponse[]>()
  const [variantsLoading, setVariantsLoading] = useState(false)
  const variantsAbort = useRef<AbortController | null>(null)

  const [projects, setProjects] = useState<Project[]>([])
  const [projectId, setProjectId] = useState('')
  const [saveState, setSaveState] = useState<string>()

  const busy = stream.running || variantsLoading

  useEffect(() => {
    api.listProjects().then((p) => {
      setProjects(p)
      setProjectId((current) => current || p[0]?.id || '')
    }, () => {})
  }, [])

  useEffect(() => () => variantsAbort.current?.abort(), [])

  const brief = (): GenerateContentRequest => ({
    prompt,
    type,
    title: title || undefined,
    targetAudience: audience || undefined,
    toneOfVoice: tone || undefined,
    language: language || undefined,
    keywords: parseKeywords(keywords),
    projectId: projectId || undefined,
  })

  const project = projects.find((p) => p.id === projectId)

  /** Shows a result and switches the keyword field to the language the API scored in. */
  function showResult(next: GenerateContentResponse) {
    setResult(next)
    const translated = translateKeywordField(keywords, next.termTranslations)
    if (translated) {
      setKeywords(translated.value)
      setNotice(`SEO keywords now match the text’s language: ${translated.changed.join(', ')}`)
    }
  }

  function resetOutput() {
    setError(undefined)
    setNotice(undefined)
    setSaveState(undefined)
    setResult(undefined)
    setHistory([])
    setVariants(undefined)
    setEditing(false)
  }

  async function generate(e: FormEvent) {
    e.preventDefault()
    if (busy) return
    resetOutput()
    setStreamLabel('Writing…')

    const outcome = await stream.run((onDelta, signal) => api.generateContentStream(brief(), onDelta, signal))
    if (outcome?.kind === 'done') {
      showResult(outcome.result)
    } else if (outcome?.kind === 'stopped' && outcome.partial.body) {
      // Keep what was written so far; it can still be edited and saved.
      setResult({
        title: outcome.partial.title || title || 'Untitled',
        body: outcome.partial.body,
        wordCount: outcome.partial.body.split(/\s+/).filter(Boolean).length,
        provider: '',
        stopped: true,
      })
    } else if (outcome?.kind === 'error') {
      setError(outcome.message)
    }
  }

  async function generateVariants() {
    if (busy || !prompt.trim()) return
    resetOutput()
    const controller = new AbortController()
    variantsAbort.current = controller
    setVariantsLoading(true)
    try {
      setVariants(await api.generateVariants(brief(), VARIANT_COUNT, controller.signal))
    } catch (err) {
      if (!controller.signal.aborted) setError((err as Error).message)
    } finally {
      variantsAbort.current = null
      setVariantsLoading(false)
    }
  }

  function chooseVariant(variant: GenerateContentResponse) {
    showResult(variant)
    setVariants(undefined)
  }

  async function runTool(action: TransformAction, options?: TransformOptions) {
    if (!result || busy) return
    const before = result
    setEditing(false)
    setError(undefined)
    setNotice(undefined)
    setSaveState(undefined)
    setStreamLabel(`${TRANSFORM_LABELS[action]}…`)

    const outcome = await stream.run((onDelta, signal) =>
      api.transformContentStream(
        {
          action,
          title: before.title,
          body: before.body,
          type,
          keywords: parseKeywords(keywords),
          projectId: projectId || undefined,
          ...options,
        },
        onDelta,
        signal,
      ),
    )
    if (outcome?.kind === 'done') {
      setHistory((h) => [...h, before])
      showResult(outcome.result)
    } else if (outcome?.kind === 'stopped') {
      setNotice('Edit stopped. Your text is unchanged.')
    } else if (outcome?.kind === 'error') {
      setError(outcome.message)
    }
  }

  function undo() {
    const previous = history[history.length - 1]
    if (!previous) return
    setHistory(history.slice(0, -1))
    setResult(previous)
    setNotice(undefined)
  }

  function stop(e?: MouseEvent) {
    stream.stop(e)
    variantsAbort.current?.abort()
  }

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
      setSaveState(`Saved to “${project?.name}”`)
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
          Project
          <select value={projectId} onChange={(e) => setProjectId(e.target.value)}>
            <option value="">No project</option>
            {projects.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
                {p.brandVoice ? ' · brand voice' : ''}
              </option>
            ))}
          </select>
          {project?.brandVoice ? (
            <span className="brand-hint on" title={project.brandVoice.voice ?? undefined}>
              ✓ Using this project’s brand voice
            </span>
          ) : project ? (
            <span className="brand-hint">
              No brand voice yet. <a href="#/projects">Add one in Projects</a>
            </span>
          ) : null}
        </label>

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
              <option value="">Neutral</option>
              {TONES.map((t) => (
                <option key={t}>{t}</option>
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

        <div className="form-actions">
          {busy ? (
            <button key="stop" type="button" className="primary stop" onClick={stop}>
              <span className="spinner" /> Stop
            </button>
          ) : (
            <>
              <button key="generate" className="primary" disabled={!prompt.trim()}>
                ✨ Generate content
              </button>
              <button type="button" className="ghost" disabled={!prompt.trim()} onClick={generateVariants}>
                Generate {VARIANT_COUNT} variants to compare
              </button>
            </>
          )}
        </div>
        {error && <p className="error">{error}</p>}
      </form>

      <section className="card result">
        {stream.live ? (
          <>
            <div className="result-toolbar">
              <span className="pill neutral">{streamLabel}</span>
              <div className="spacer" />
              <button type="button" className="ghost" onClick={stop}>
                ■ Stop
              </button>
            </div>
            <article className="streaming">
              {stream.live.title && <h1 className="result-title">{stream.live.title}</h1>}
              {stream.live.body ? <Markdown text={stream.live.body} /> : <span className="cursor" />}
            </article>
          </>
        ) : variantsLoading ? (
          <div className="empty">
            <div className="empty-icon">
              <span className="spinner large" />
            </div>
            <p>Writing {VARIANT_COUNT} variants in parallel…</p>
            <p className="muted small">Each takes a different angle. This can take as long as a single long generation.</p>
          </div>
        ) : variants ? (
          <>
            <div className="result-toolbar">
              <h2 className="no-margin">Pick a variant</h2>
              <div className="spacer" />
              <span className="pill neutral">via {variants[0]?.provider}</span>
            </div>
            <ul className="variants">
              {variants.map((v, i) => (
                <li key={i} className="variant">
                  <span className="muted small">Variant {i + 1}</span>
                  <h3>{v.title}</h3>
                  <p className="muted small snippet">{snippet(v.body)}…</p>
                  <div className="variant-meta">
                    <span className="muted small">{v.wordCount} words</span>
                    <button type="button" className="primary" onClick={() => chooseVariant(v)}>
                      Use this
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </>
        ) : !result ? (
          <div className="empty">
            <div className="empty-icon">✍️</div>
            <p>
              Fill in the brief and hit <strong>Generate</strong>.
            </p>
            <p className="muted small">
              Generated content appears here with SEO insights. Refine it with AI tools, edit it and save it to a project.
            </p>
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
              {history.length > 0 && (
                <button type="button" className="ghost" onClick={undo} title="Undo the last AI edit">
                  ↶ Undo
                </button>
              )}
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

            <AiTools disabled={busy} onRun={runTool} />
            {notice && <p className="notice">{notice}</p>}

            {result.stopped || !result.seo ? (
              <p className="muted small">Generation was stopped, so SEO analysis isn’t available. You can still edit and save the text.</p>
            ) : (
              <SeoPanel report={result.seo} translations={result.termTranslations} />
            )}
            {result.brandCheck && <BrandCheckPanel check={result.brandCheck} translations={result.termTranslations} />}

            <div className="save-bar">
              {project ? (
                <span className="muted small">
                  Project: <strong>{project.name}</strong>
                </span>
              ) : (
                <span className="muted small">Choose a project in the brief to save this.</span>
              )}
              <div className="spacer" />
              <button type="button" className="primary" onClick={save} disabled={!project}>
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
