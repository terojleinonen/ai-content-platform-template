import type {
  BrandVoice,
  ContentItem,
  GenerateContentRequest,
  GenerateContentResponse,
  GenerateImageRequest,
  GenerateImageResponse,
  Health,
  Project,
  SaveContentItem,
  TransformContentRequest,
  UsageReport,
} from './types'

const BASE = import.meta.env.VITE_API_BASE ?? ''

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function errorMessage(res: Response): Promise<string> {
  let message = `${res.status} ${res.statusText}`
  try {
    const problem = (await res.json()) as ProblemDetails
    if (problem.errors) message = Object.values(problem.errors).flat().join(' ')
    else message = problem.detail ?? problem.title ?? message
  } catch {
    // Non-JSON error body; keep the status text.
  }
  return message
}

async function request<T>(method: string, path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  let res: Response
  try {
    res = await fetch(BASE + path, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (err) {
    if (signal?.aborted) throw err
    throw new Error('Cannot reach the API. Is the backend running on http://localhost:5080?')
  }

  if (!res.ok) throw new Error(await errorMessage(res))

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T)
}

export type OnDelta = (text: string) => void

/**
 * POSTs to a streaming endpoint and reads its Server-Sent Events. Calls `onDelta` with each
 * Markdown chunk and resolves with the final response (title, body, SEO). Abort with `signal` to stop.
 */
async function streamContent(
  path: string,
  body: unknown,
  onDelta: OnDelta,
  signal?: AbortSignal,
): Promise<GenerateContentResponse> {
  let res: Response
  try {
    res = await fetch(BASE + path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
      body: JSON.stringify(body),
      signal,
    })
  } catch (err) {
    if (signal?.aborted) throw err
    throw new Error('Cannot reach the API. Is the backend running on http://localhost:5080?')
  }
  if (!res.ok || !res.body) throw new Error(await errorMessage(res))

  const reader = res.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  for (;;) {
    const { value, done } = await reader.read()
    if (done) break
    buffer += value

    // Events are separated by a blank line; keep any trailing partial event in the buffer.
    const events = buffer.split(/\r?\n\r?\n/)
    buffer = events.pop() ?? ''

    for (const raw of events) {
      let type = 'message'
      const data: string[] = []
      for (const line of raw.split(/\r?\n/)) {
        if (line.startsWith('event:')) type = line.slice(6).trim()
        else if (line.startsWith('data:')) data.push(line.slice(5).trimStart())
      }
      if (!data.length) continue
      const payload = JSON.parse(data.join('\n'))

      if (type === 'delta') onDelta(payload.text)
      else if (type === 'done') return payload as GenerateContentResponse
      else if (type === 'error') throw new Error(payload.message)
    }
  }
  throw new Error('The stream ended unexpectedly.')
}

export const api = {
  health: () => request<Health>('GET', '/api/health'),

  generateContent: (req: GenerateContentRequest) =>
    request<GenerateContentResponse>('POST', '/api/content/generate', req),
  generateContentStream: (req: GenerateContentRequest, onDelta: OnDelta, signal?: AbortSignal) =>
    streamContent('/api/content/generate/stream', req, onDelta, signal),
  generateVariants: (req: GenerateContentRequest, count: number, signal?: AbortSignal) =>
    request<GenerateContentResponse[]>('POST', `/api/content/variants?count=${count}`, req, signal),
  transformContentStream: (req: TransformContentRequest, onDelta: OnDelta, signal?: AbortSignal) =>
    streamContent('/api/content/transform/stream', req, onDelta, signal),
  generateImage: (req: GenerateImageRequest) =>
    request<GenerateImageResponse>('POST', '/api/image/generate', req),

  listProjects: () => request<Project[]>('GET', '/api/projects'),
  createProject: (name: string, description?: string) =>
    request<Project>('POST', '/api/projects', { name, description }),
  deleteProject: (id: string) => request<void>('DELETE', `/api/projects/${id}`),
  saveBrandVoice: (id: string, brand: BrandVoice) => request<Project>('PUT', `/api/projects/${id}/brand-voice`, brand),

  listContent: (projectId: string) => request<ContentItem[]>('GET', `/api/projects/${projectId}/content`),
  addContent: (projectId: string, item: SaveContentItem) =>
    request<ContentItem>('POST', `/api/projects/${projectId}/content`, item),
  updateContent: (id: string, item: SaveContentItem) =>
    request<ContentItem>('PUT', `/api/content-items/${id}`, item),
  deleteContent: (id: string) => request<void>('DELETE', `/api/content-items/${id}`),

  usage: (days: number) => request<UsageReport>('GET', `/api/usage?days=${days}`),
}

/**
 * Rewrites a comma-separated keyword field with the translations the API used, so follow-up
 * edits and SEO scoring stay in the content's language. Returns null when nothing changed.
 */
export function translateKeywordField(value: string, translations?: Record<string, string> | null) {
  if (!translations) return null
  const lookup = new Map(Object.entries(translations).map(([k, v]) => [k.toLowerCase(), v]))
  const changed: string[] = []
  const keywords = parseKeywords(value).map((k) => {
    const translated = lookup.get(k.toLowerCase())
    if (!translated) return k
    changed.push(`${k} → ${translated}`)
    return translated
  })
  return changed.length ? { value: keywords.join(', '), changed } : null
}

/** Maps translated term → original, for showing the original next to a translated term. */
export const originalsOf = (translations?: Record<string, string> | null) =>
  new Map(Object.entries(translations ?? {}).map(([original, translated]) => [translated.toLowerCase(), original]))

export const parseKeywords = (value: string) =>
  value
    .split(',')
    .map((k) => k.trim())
    .filter(Boolean)
