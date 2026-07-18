import type {
  CodeSnippet,
  CodeSnippetListParams,
  CreateCodeSnippetRequest,
  UpdateCodeSnippetRequest,
  CodeSnippetListResponse,
  FavoriteSnippetRequest
} from '@/types/codeSnippet'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

function parseCodeSnippet(data: Record<string, unknown>): CodeSnippet {
  return {
    ...data,
    createdAt: new Date(data.createdAt as string),
    updatedAt: new Date(data.updatedAt as string),
    lastUsedAt: data.lastUsedAt ? new Date(data.lastUsedAt as string) : undefined,
    tags: (data.tags as string[]) || []
  } as CodeSnippet
}

export const codeSnippetApi = {
  async listSnippets(params?: CodeSnippetListParams): Promise<CodeSnippetListResponse> {
    const queryParams = new URLSearchParams()
    if (params?.keyword) {
      queryParams.append('keyword', params.keyword)
    }
    if (params?.language) {
      queryParams.append('language', params.language)
    }
    if (params?.category) {
      queryParams.append('category', params.category)
    }
    if (params?.isFavorite !== undefined) {
      queryParams.append('isFavorite', String(params.isFavorite))
    }
    if (params?.page !== undefined) {
      queryParams.append('page', String(params.page))
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize))
    }

    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/codesnippets${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`获取代码片段列表失败: ${response.status}`)
    }

    const data = await response.json()
    return {
      items: (data.items || data.data || []).map(parseCodeSnippet),
      total: data.total ?? 0,
      page: data.page ?? 1,
      pageSize: data.pageSize ?? 20
    }
  },

  async getSnippet(id: number): Promise<CodeSnippet> {
    const response = await fetch(`${API_BASE_URL}/codesnippets/${id}`)

    if (!response.ok) {
      throw new Error(`获取代码片段详情失败: ${response.status}`)
    }

    const data = await response.json()
    return parseCodeSnippet(data.data || data)
  },

  async createSnippet(request: CreateCodeSnippetRequest): Promise<CodeSnippet> {
    const response = await fetch(`${API_BASE_URL}/codesnippets`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`创建代码片段失败: ${response.status}`)
    }

    const data = await response.json()
    return parseCodeSnippet(data.data || data)
  },

  async updateSnippet(id: number, request: UpdateCodeSnippetRequest): Promise<CodeSnippet> {
    const response = await fetch(`${API_BASE_URL}/codesnippets/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`更新代码片段失败: ${response.status}`)
    }

    const data = await response.json()
    return parseCodeSnippet(data.data || data)
  },

  async deleteSnippet(id: number): Promise<boolean> {
    const response = await fetch(`${API_BASE_URL}/codesnippets/${id}`, {
      method: 'DELETE'
    })

    if (!response.ok) {
      throw new Error(`删除代码片段失败: ${response.status}`)
    }

    return true
  },

  async favoriteSnippet(id: number, isFavorite: boolean): Promise<boolean> {
    const request: FavoriteSnippetRequest = { isFavorite }
    const response = await fetch(`${API_BASE_URL}/codesnippets/${id}/favorite`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`收藏切换失败: ${response.status}`)
    }

    return true
  },

  async incrementUsage(id: number): Promise<boolean> {
    const response = await fetch(`${API_BASE_URL}/codesnippets/${id}/increment-usage`, {
      method: 'POST'
    })

    if (!response.ok) {
      throw new Error(`增加使用次数失败: ${response.status}`)
    }

    return true
  },

  async createFromScript(scriptId: number, title?: string): Promise<CodeSnippet> {
    const queryParams = new URLSearchParams()
    if (title) {
      queryParams.append('title', title)
    }
    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/codesnippets/from-script/${scriptId}${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url, {
      method: 'POST'
    })

    if (!response.ok) {
      throw new Error(`从脚本创建代码片段失败: ${response.status}`)
    }

    const data = await response.json()
    return parseCodeSnippet(data.data || data)
  },

  async getLanguages(): Promise<string[]> {
    const response = await fetch(`${API_BASE_URL}/codesnippets/languages`)

    if (!response.ok) {
      throw new Error(`获取语言列表失败: ${response.status}`)
    }

    const data = await response.json()
    return data.data || data || []
  },

  async getCategories(): Promise<string[]> {
    const response = await fetch(`${API_BASE_URL}/codesnippets/categories`)

    if (!response.ok) {
      throw new Error(`获取分类列表失败: ${response.status}`)
    }

    const data = await response.json()
    return data.data || data || []
  }
}
