export const CONTENT_TYPES = ['BlogPost', 'ProductDescription', 'SocialPost', 'Email', 'Custom'] as const
export type ContentType = (typeof CONTENT_TYPES)[number]

export const CONTENT_TYPE_LABELS: Record<ContentType, string> = {
  BlogPost: 'Blog post',
  ProductDescription: 'Product description',
  SocialPost: 'Social post',
  Email: 'Email',
  Custom: 'Custom',
}

export const TONES = ['Friendly', 'Professional', 'Playful', 'Persuasive'] as const
export const LANGUAGES = ['English', 'Finnish', 'Swedish', 'German', 'French', 'Spanish'] as const

export interface CurrentUser {
  id: string
  email: string
  displayName: string
  hasPassword: boolean
  externalLogins: string[]
}

export interface Health {
  status: string
  textProvider: string
  textModel: string
  imageProvider: string
}

export interface BrandVoice {
  voice?: string | null
  targetAudience?: string | null
  keyFacts?: string | null
  preferredTerms: string[]
  avoidTerms: string[]
}

export interface KeywordInsight {
  keyword: string
  occurrences: number
  density: number
  rating: 'missing' | 'low' | 'good' | 'too high' | 'too many'
}

/** Keyword usage rated for the content's type and length: by mentions (short) or density (long). */
export interface SeoReport {
  mode: 'Density' | 'Mentions'
  target: string
  wordCount: number
  keywords: KeywordInsight[]
}

export interface BrandCheck {
  projectName: string
  avoidTermsFound: string[]
  preferredTermsUsed: string[]
  preferredTermsMissing: string[]
}

export interface GenerateContentRequest {
  prompt: string
  projectId?: string
  type: ContentType
  title?: string
  targetAudience?: string
  toneOfVoice?: string
  language?: string
  keywords?: string[]
}

export interface GenerateContentResponse {
  title: string
  body: string
  seoSummary?: string
  keywordScores?: Record<string, number>
  seo?: SeoReport | null
  wordCount: number
  provider: string
  brandCheck?: BrandCheck | null
  /** Keywords/brand terms translated into the content's language: original → translated. */
  termTranslations?: Record<string, string> | null
  usage?: AiUsageSummary | null
}

export interface AiUsageSummary {
  model: string
  inputTokens: number
  outputTokens: number
  costUsd?: number | null
  estimated: boolean
}

export type AiOperation = 'Generate' | 'Variant' | 'Transform' | 'TermTranslation' | 'Image'
export type AiCallStatus = 'Succeeded' | 'Failed' | 'Cancelled'

export interface UsageGroup {
  key: string
  label: string
  calls: number
  tokens: number
  costUsd: number
}

export interface UsageReport {
  from: string
  to: string
  totals: { calls: number; inputTokens: number; outputTokens: number; costUsd: number; unpricedCalls: number; failedCalls: number }
  byDay: { date: string; calls: number; tokens: number; costUsd: number }[]
  byOperation: UsageGroup[]
  byProject: UsageGroup[]
  byModel: UsageGroup[]
  recent: {
    id: string
    createdAt: string
    operation: AiOperation
    detail?: string | null
    projectName?: string | null
    provider: string
    model: string
    inputTokens: number
    outputTokens: number
    estimated: boolean
    costUsd?: number | null
    durationMs: number
    status: AiCallStatus
  }[]
}

export const TRANSFORM_ACTIONS = ['Improve', 'Shorten', 'Expand', 'ChangeTone', 'Translate', 'Custom'] as const
export type TransformAction = (typeof TRANSFORM_ACTIONS)[number]

export interface TransformContentRequest {
  action: TransformAction
  title?: string
  body: string
  type?: ContentType
  toneOfVoice?: string
  language?: string
  instruction?: string
  keywords?: string[]
  projectId?: string
}

export interface GenerateImageRequest {
  prompt: string
  style?: string
  width: number
  height: number
}

export interface GenerateImageResponse {
  url: string
  promptUsed?: string
  provider: string
}

export interface Project {
  id: string
  name: string
  description?: string
  createdAt: string
  contentCount: number
  brandVoice?: BrandVoice | null
}

export interface ContentItem {
  id: string
  projectId: string
  type: ContentType
  title: string
  body: string
  targetAudience?: string
  toneOfVoice?: string
  keywords: string[]
  createdAt: string
  updatedAt?: string
}

export interface SaveContentItem {
  type: ContentType
  title: string
  body: string
  targetAudience?: string
  toneOfVoice?: string
  keywords?: string[]
}
