/**
 * 快捷链接API服务层 - 封装快捷链接管理相关后端通信
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
const QUICK_LINKS_BASE = `${API_BASE_URL}/quick-links`

export const quickLinksApi = {
  /**
   * 获取链接列表
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
    const url = `${QUICK_LINKS_BASE}/links${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`获取链接列表失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 获取单个链接
   */
  async fetchLinkById(id: string): Promise<QuickLink> {
    const response = await fetch(`${QUICK_LINKS_BASE}/links/${id}`)

    if (!response.ok) {
      throw new Error(`获取链接详情失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 创建链接
   */
  async createLink(link: QuickLinkCreateRequest): Promise<QuickLink> {
    const response = await fetch(`${QUICK_LINKS_BASE}/links`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(link)
    })

    if (!response.ok) {
      throw new Error(`创建链接失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 更新链接
   */
  async updateLink(id: string, link: QuickLinkUpdateRequest): Promise<QuickLink> {
    const response = await fetch(`${QUICK_LINKS_BASE}/links/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(link)
    })

    if (!response.ok) {
      throw new Error(`更新链接失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 删除链接
   */
  async deleteLink(id: string): Promise<void> {
    const response = await fetch(`${QUICK_LINKS_BASE}/links/${id}`, {
      method: 'DELETE'
    })

    if (!response.ok) {
      throw new Error(`删除链接失败: ${response.status}`)
    }
  },

  /**
   * 记录点击
   */
  async recordClick(id: string): Promise<void> {
    const response = await fetch(`${QUICK_LINKS_BASE}/links/${id}/click`, {
      method: 'POST'
    })

    if (!response.ok) {
      throw new Error(`记录点击失败: ${response.status}`)
    }
  },

  /**
   * 重新排序链接
   */
  async reorderLinks(orderedIds: string[]): Promise<void> {
    const response = await fetch(`${QUICK_LINKS_BASE}/links/reorder`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ orderedIds })
    })

    if (!response.ok) {
      throw new Error(`重新排序失败: ${response.status}`)
    }
  },

  /**
   * 获取分类列表
   */
  async fetchCategories(): Promise<QuickLinkCategory[]> {
    const response = await fetch(`${QUICK_LINKS_BASE}/categories`)

    if (!response.ok) {
      throw new Error(`获取分类列表失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 创建分类
   */
  async createCategory(category: CategoryCreateRequest): Promise<QuickLinkCategory> {
    const response = await fetch(`${QUICK_LINKS_BASE}/categories`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(category)
    })

    if (!response.ok) {
      throw new Error(`创建分类失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 更新分类
   */
  async updateCategory(id: string, category: CategoryUpdateRequest): Promise<QuickLinkCategory> {
    const response = await fetch(`${QUICK_LINKS_BASE}/categories/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(category)
    })

    if (!response.ok) {
      throw new Error(`更新分类失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 删除分类
   */
  async deleteCategory(id: string): Promise<void> {
    const response = await fetch(`${QUICK_LINKS_BASE}/categories/${id}`, {
      method: 'DELETE'
    })

    if (!response.ok) {
      throw new Error(`删除分类失败: ${response.status}`)
    }
  },

  /**
   * 导入链接
   */
  async importLinks(links: QuickLinkCreateRequest[], mode: ImportMode): Promise<QuickLink[]> {
    const response = await fetch(`${QUICK_LINKS_BASE}/links/import`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ links, mode })
    })

    if (!response.ok) {
      throw new Error(`导入链接失败: ${response.status}`)
    }

    return response.json()
  },

  /**
   * 导出链接
   */
  async exportLinks(categoryId?: string): Promise<QuickLink[]> {
    const queryParams = new URLSearchParams()
    if (categoryId) {
      queryParams.append('categoryId', categoryId)
    }

    const queryString = queryParams.toString()
    const url = `${QUICK_LINKS_BASE}/links/export${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`导出链接失败: ${response.status}`)
    }

    return response.json()
  }
}
