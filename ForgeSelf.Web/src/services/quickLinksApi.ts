/**
 * 快捷链接API服务层 - 封装快捷链接管理相关后端通信
 *
 * 注意：后端路由前缀为 `api/quicklinks`（无连字符），列表/详情等均在根路径，
 * 分类为 `api/quicklinks/categories`；响应为 ApiResponse<T> 包装（取 .data）。
 */

import type {
  QuickLink,
  QuickLinkCategory,
  QuickLinkQueryParams,
  QuickLinkCreateRequest,
  QuickLinkUpdateRequest,
  CategoryCreateRequest,
  CategoryUpdateRequest,
  ImportMode
} from '@/types/quickLinks'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'
const QUICK_LINKS_BASE = `${API_BASE_URL}/quicklinks`

/** 解析后端 ApiResponse<T> 包装，返回 data */
async function parseData<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = `请求失败: ${response.status}`
    try {
      const json = await response.json()
      if (json?.message) message = json.message
    } catch {
      // ignore parse error
    }
    throw new Error(message)
  }
  const json = await response.json()
  return (json?.data !== undefined ? json.data : json) as T
}

export const quickLinksApi = {
  /**
   * 获取链接列表（GET /api/quicklinks，返回 PagedResult，取 data.items）
   */
  async fetchLinks(params?: QuickLinkQueryParams): Promise<QuickLink[]> {
    const queryParams = new URLSearchParams()
    if (params?.keyword) {
      queryParams.append('keyword', params.keyword)
    }
    if (params?.categoryId) {
      queryParams.append('categoryId', params.categoryId)
    }
    if (params?.page !== undefined) {
      queryParams.append('page', String(params.page))
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize))
    }

    const queryString = queryParams.toString()
    const url = `${QUICK_LINKS_BASE}${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)
    const data = await parseData<{ items: QuickLink[] }>(response)
    return data.items ?? []
  },

  /**
   * 获取单个链接（GET /api/quicklinks/{id}）
   */
  async fetchLinkById(id: string): Promise<QuickLink> {
    const response = await fetch(`${QUICK_LINKS_BASE}/${id}`)
    return parseData<QuickLink>(response)
  },

  /**
   * 创建链接（POST /api/quicklinks）
   */
  async createLink(link: QuickLinkCreateRequest): Promise<QuickLink> {
    const response = await fetch(`${QUICK_LINKS_BASE}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(link)
    })
    return parseData<QuickLink>(response)
  },

  /**
   * 更新链接（PUT /api/quicklinks/{id}）
   */
  async updateLink(id: string, link: QuickLinkUpdateRequest): Promise<QuickLink> {
    const response = await fetch(`${QUICK_LINKS_BASE}/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(link)
    })
    return parseData<QuickLink>(response)
  },

  /**
   * 删除链接（DELETE /api/quicklinks/{id}）
   */
  async deleteLink(id: string): Promise<void> {
    const response = await fetch(`${QUICK_LINKS_BASE}/${id}`, {
      method: 'DELETE'
    })
    await parseData<unknown>(response)
  },

  /**
   * 记录点击（POST /api/quicklinks/{id}/click）
   */
  async recordClick(id: string): Promise<void> {
    const response = await fetch(`${QUICK_LINKS_BASE}/${id}/click`, {
      method: 'POST'
    })
    await parseData<unknown>(response)
  },

  /**
   * 重新排序链接（POST /api/quicklinks/reorder）
   */
  async reorderLinks(orderedIds: string[]): Promise<void> {
    const response = await fetch(`${QUICK_LINKS_BASE}/reorder`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ orderedIds })
    })
    await parseData<unknown>(response)
  },

  /**
   * 获取分类列表（GET /api/quicklinks/categories，返回数组）
   */
  async fetchCategories(): Promise<QuickLinkCategory[]> {
    const response = await fetch(`${QUICK_LINKS_BASE}/categories`)
    const data = await parseData<QuickLinkCategory[]>(response)
    return data ?? []
  },

  /**
   * 创建分类（POST /api/quicklinks/categories）
   */
  async createCategory(category: CategoryCreateRequest): Promise<QuickLinkCategory> {
    const response = await fetch(`${QUICK_LINKS_BASE}/categories`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(category)
    })
    return parseData<QuickLinkCategory>(response)
  },

  /**
   * 更新分类（PUT /api/quicklinks/categories/{id}）
   */
  async updateCategory(id: string, category: CategoryUpdateRequest): Promise<QuickLinkCategory> {
    const response = await fetch(`${QUICK_LINKS_BASE}/categories/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(category)
    })
    return parseData<QuickLinkCategory>(response)
  },

  /**
   * 删除分类（DELETE /api/quicklinks/categories/{id}）
   */
  async deleteCategory(id: string): Promise<void> {
    const response = await fetch(`${QUICK_LINKS_BASE}/categories/${id}`, {
      method: 'DELETE'
    })
    await parseData<unknown>(response)
  },

  /**
   * 导入链接（POST /api/quicklinks/import）
   */
  async importLinks(links: QuickLinkCreateRequest[], mode: ImportMode): Promise<QuickLink[]> {
    const response = await fetch(`${QUICK_LINKS_BASE}/import`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ links, mode })
    })
    return parseData<QuickLink[]>(response)
  },

  /**
   * 导出链接（GET /api/quicklinks/export）
   */
  async exportLinks(categoryId?: string): Promise<QuickLink[]> {
    const queryParams = new URLSearchParams()
    if (categoryId) {
      queryParams.append('categoryId', categoryId)
    }

    const queryString = queryParams.toString()
    const url = `${QUICK_LINKS_BASE}/export${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)
    return parseData<QuickLink[]>(response)
  }
}
