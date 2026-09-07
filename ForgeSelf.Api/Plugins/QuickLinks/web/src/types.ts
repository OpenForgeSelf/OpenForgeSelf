/**
 * 快捷链接类型定义（插件自带界面，从宿主 @/types/quickLinks 移植）
 */

export interface QuickLink {
  id: string
  name: string
  url: string
  icon: string
  description: string
  categoryId: string
  sortOrder: number
  clickCount: number
  createdAt: string
  updatedAt: string
}

export interface QuickLinkCategory {
  id: string
  name: string
  icon: string
  sortOrder: number
  createdAt: string
}

export interface QuickLinkQueryParams {
  keyword?: string
  categoryId?: string
  page?: number
  pageSize?: number
}

export type ImportMode = 'append' | 'replace'

export interface QuickLinkCreateRequest {
  name: string
  url: string
  icon?: string
  description?: string
  categoryId: string
}

export interface QuickLinkUpdateRequest {
  name?: string
  url?: string
  icon?: string
  description?: string
  categoryId?: string
  sortOrder?: number
}

export interface CategoryCreateRequest {
  name: string
  icon?: string
}

export interface CategoryUpdateRequest {
  name?: string
  icon?: string
  sortOrder?: number
}

export interface ImportLinksRequest {
  links: QuickLinkCreateRequest[]
  mode: ImportMode
}
