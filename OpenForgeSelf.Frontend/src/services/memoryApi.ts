import type {
  Memory,
  MemoryCategory,
  MemoryStats,
  MemorySearchResult,
  CreateMemoryRequest,
  UpdateMemoryRequest,
  SearchMemoryRequest,
  CreateMemoryCategoryRequest,
  UpdateMemoryCategoryRequest,
  ImportMemoryRequest
} from '@/types/memory'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

interface ApiResponse<T = unknown> {
  data?: T
  message?: string
}

function unwrap<T>(response: Response): Promise<T> {
  return response.json().then((data: ApiResponse<T>) => {
    if (!response.ok) {
      throw new Error(data.message || `请求失败: ${response.status}`)
    }
    return (data.data ?? data) as T
  })
}

export const memoryApi = {
  async searchMemories(params: SearchMemoryRequest): Promise<MemorySearchResult> {
    const queryParams = new URLSearchParams()
    if (params.keyword) queryParams.append('keyword', params.keyword)
    if (params.categoryId !== undefined && params.categoryId !== null) queryParams.append('categoryId', String(params.categoryId))
    if (params.type !== undefined && params.type !== null) queryParams.append('type', String(params.type))
    if (params.minImportance !== undefined && params.minImportance !== null) queryParams.append('minImportance', String(params.minImportance))
    if (params.page) queryParams.append('page', String(params.page))
    if (params.pageSize) queryParams.append('pageSize', String(params.pageSize))

    const response = await fetch(`${API_BASE_URL}/memory/search?${queryParams}`)
    return unwrap<MemorySearchResult>(response)
  },

  async getMemory(id: number): Promise<Memory> {
    const response = await fetch(`${API_BASE_URL}/memory/${id}`)
    return unwrap<Memory>(response)
  },

  async createMemory(request: CreateMemoryRequest): Promise<Memory> {
    const response = await fetch(`${API_BASE_URL}/memory`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    })
    return unwrap<Memory>(response)
  },

  async updateMemory(id: number, request: UpdateMemoryRequest): Promise<Memory> {
    const response = await fetch(`${API_BASE_URL}/memory/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    })
    return unwrap<Memory>(response)
  },

  async deleteMemory(id: number): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/memory/${id}`, { method: 'DELETE' })
    if (!response.ok) {
      throw new Error('删除失败')
    }
  },

  async getCategories(): Promise<MemoryCategory[]> {
    const response = await fetch(`${API_BASE_URL}/memory/categories`)
    return unwrap<MemoryCategory[]>(response)
  },

  async createCategory(request: CreateMemoryCategoryRequest): Promise<MemoryCategory> {
    const response = await fetch(`${API_BASE_URL}/memory/categories`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    })
    return unwrap<MemoryCategory>(response)
  },

  async updateCategory(id: number, request: UpdateMemoryCategoryRequest): Promise<MemoryCategory> {
    const response = await fetch(`${API_BASE_URL}/memory/categories/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    })
    return unwrap<MemoryCategory>(response)
  },

  async deleteCategory(id: number): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/memory/categories/${id}`, { method: 'DELETE' })
    if (!response.ok) {
      throw new Error('删除失败')
    }
  },

  async getStats(): Promise<MemoryStats> {
    const response = await fetch(`${API_BASE_URL}/memory/stats`)
    return unwrap<MemoryStats>(response)
  },

  async exportMemories(format = 'json'): Promise<Blob> {
    const response = await fetch(`${API_BASE_URL}/memory/export?format=${format}`)
    if (!response.ok) {
      throw new Error('导出失败')
    }
    return response.blob()
  },

  async importMemories(request: ImportMemoryRequest): Promise<{ imported: number; skipped: number }> {
    const response = await fetch(`${API_BASE_URL}/memory/import`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    })
    return unwrap<{ imported: number; skipped: number }>(response)
  },

  async getRelevantMemories(query: string, limit = 10): Promise<Memory[]> {
    const response = await fetch(`${API_BASE_URL}/memory/relevant?query=${encodeURIComponent(query)}&limit=${limit}`)
    return unwrap<Memory[]>(response)
  }
}
