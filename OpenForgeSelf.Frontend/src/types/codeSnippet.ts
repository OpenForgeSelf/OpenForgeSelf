export type CodeSnippetSource = 'Manual' | 'Script' | 'AIGenerated'

export interface CodeSnippet {
  id: number
  title: string
  description: string
  code: string
  language: string
  category: string
  tags: string[]
  isFavorite: boolean
  createdAt: Date
  updatedAt: Date
  usageCount: number
  lastUsedAt?: Date
  source: CodeSnippetSource
  sourceScriptId?: number
}

export interface CodeSnippetListParams {
  keyword?: string
  language?: string
  category?: string
  isFavorite?: boolean
  page?: number
  pageSize?: number
}

export interface CreateCodeSnippetRequest {
  title: string
  description?: string
  code: string
  language: string
  category?: string
  tags?: string[]
  source?: CodeSnippetSource
}

export interface UpdateCodeSnippetRequest {
  title?: string
  description?: string
  code?: string
  language?: string
  category?: string
  tags?: string[]
}

export interface CodeSnippetListResponse {
  items: CodeSnippet[]
  total: number
  page: number
  pageSize: number
}

export interface FavoriteSnippetRequest {
  isFavorite: boolean
}
