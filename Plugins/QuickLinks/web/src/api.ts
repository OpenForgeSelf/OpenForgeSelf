/**
 * 快捷链接 API 服务层（插件自带界面，从宿主 services/quickLinksApi 移植）。
 *
 * 与宿主实现不同点：
 * - 直连后端相对路径 `/api/quicklinks*`（插件与宿主同源，无需 VITE_API_BASE_URL）。
 * - 鉴权复用 `./http.ts` 的 `apiGet/apiPost/apiDelete`（从 localStorage 读宿主写入的 token）。
 * - 后端响应为标准信封 { code/message/data } 或裸对象，由 http.ts 统一解包到 data。
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
} from './types'
import { apiGet, apiPost, apiPut, apiDelete } from './http'

const BASE = '/api/quicklinks'

function asArray<T>(data: T[] | undefined): T[] {
  return Array.isArray(data) ? data : []
}

export const quickLinksApi = {
  async fetchLinks(params?: QuickLinkQueryParams): Promise<QuickLink[]> {
    const qs = new URLSearchParams()
    if (params?.keyword) qs.append('keyword', params.keyword)
    if (params?.categoryId) qs.append('categoryId', params.categoryId)
    if (params?.page !== undefined) qs.append('page', String(params.page))
    if (params?.pageSize !== undefined) qs.append('pageSize', String(params.pageSize))
    const q = qs.toString()
    const data = await apiGet<{ items: QuickLink[] }>(`${BASE}${q ? `?${q}` : ''}`)
    return data?.items ?? []
  },

  async fetchLinkById(id: string): Promise<QuickLink> {
    return (await apiGet<QuickLink>(`${BASE}/${id}`)) as QuickLink
  },

  async createLink(link: QuickLinkCreateRequest): Promise<QuickLink> {
    return (await apiPost<QuickLink>(BASE, link)) as QuickLink
  },

  // 后端为 [HttpPut("{id}")]，必须是 PUT，POST 会 405
  async updateLink(id: string, link: QuickLinkUpdateRequest): Promise<QuickLink> {
    return (await apiPut<QuickLink>(`${BASE}/${id}`, link)) as QuickLink
  },

  async deleteLink(id: string): Promise<void> {
    await apiDelete(`${BASE}/${id}`)
  },

  async recordClick(id: string): Promise<void> {
    await apiPost(`${BASE}/${id}/click`, {})
  },

  async reorderLinks(orderedIds: string[]): Promise<void> {
    await apiPost(`${BASE}/reorder`, { orderedIds })
  },

  async fetchCategories(): Promise<QuickLinkCategory[]> {
    const data = await apiGet<QuickLinkCategory[]>(`${BASE}/categories`)
    return asArray(data)
  },

  async createCategory(category: CategoryCreateRequest): Promise<QuickLinkCategory> {
    return (await apiPost<QuickLinkCategory>(`${BASE}/categories`, category)) as QuickLinkCategory
  },

  // 后端为 [HttpPut("categories/{id}")]，必须是 PUT，POST 会 405
  async updateCategory(id: string, category: CategoryUpdateRequest): Promise<QuickLinkCategory> {
    return (await apiPut<QuickLinkCategory>(`${BASE}/categories/${id}`, category)) as QuickLinkCategory
  },

  async deleteCategory(id: string): Promise<void> {
    await apiDelete(`${BASE}/categories/${id}`)
  },

  async importLinks(links: QuickLinkCreateRequest[], mode: ImportMode): Promise<QuickLink[]> {
    return asArray(await apiPost<QuickLink[]>(`${BASE}/import`, { links, mode }))
  },

  async exportLinks(categoryId?: string): Promise<QuickLink[]> {
    const q = categoryId ? `?categoryId=${encodeURIComponent(categoryId)}` : ''
    return asArray(await apiGet<QuickLink[]>(`${BASE}/export${q}`))
  }
}
