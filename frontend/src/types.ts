export const CONTENT_TYPES = ['BlogPost', 'ProductDescription', 'SocialPost', 'Email', 'Custom'] as const
export type ContentType = (typeof CONTENT_TYPES)[number]

export const CONTENT_TYPE_LABELS: Record<ContentType, string> = {
  BlogPost: 'Blog post',
  ProductDescription: 'Product description',
  SocialPost: 'Social post',
  Email: 'Email',
  Custom: 'Custom',
}

export interface Health {
  status: string
  textProvider: string
  textModel: string
  imageProvider: string
}

export interface GenerateContentRequest {
  prompt: string
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
  wordCount: number
  provider: string
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
