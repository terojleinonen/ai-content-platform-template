import type {
  ContentItem,
  GenerateContentRequest,
  GenerateContentResponse,
  GenerateImageRequest,
  GenerateImageResponse,
  Health,
  Project,
  SaveContentItem,
} from './types'

const BASE = import.meta.env.VITE_API_BASE ?? ''

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  let res: Response
  try {
    res = await fetch(BASE + path, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new Error('Cannot reach the API. Is the backend running on http://localhost:5080?')
  }

  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`
    try {
      const problem = (await res.json()) as ProblemDetails
      if (problem.errors) message = Object.values(problem.errors).flat().join(' ')
      else message = problem.detail ?? problem.title ?? message
    } catch {
      // Non-JSON error body; keep the status text.
    }
    throw new Error(message)
  }

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T)
}

export const api = {
  health: () => request<Health>('GET', '/api/health'),

  generateContent: (req: GenerateContentRequest) =>
    request<GenerateContentResponse>('POST', '/api/content/generate', req),
  generateImage: (req: GenerateImageRequest) =>
    request<GenerateImageResponse>('POST', '/api/image/generate', req),

  listProjects: () => request<Project[]>('GET', '/api/projects'),
  createProject: (name: string, description?: string) =>
    request<Project>('POST', '/api/projects', { name, description }),
  deleteProject: (id: string) => request<void>('DELETE', `/api/projects/${id}`),

  listContent: (projectId: string) => request<ContentItem[]>('GET', `/api/projects/${projectId}/content`),
  addContent: (projectId: string, item: SaveContentItem) =>
    request<ContentItem>('POST', `/api/projects/${projectId}/content`, item),
  updateContent: (id: string, item: SaveContentItem) =>
    request<ContentItem>('PUT', `/api/content-items/${id}`, item),
  deleteContent: (id: string) => request<void>('DELETE', `/api/content-items/${id}`),
}

export const parseKeywords = (value: string) =>
  value
    .split(',')
    .map((k) => k.trim())
    .filter(Boolean)
