export enum MemoryType {
  Fact = 0,
  Preference = 1,
  Project = 2,
  Personal = 3,
  Workflow = 4,
  Skill = 5,
  Other = 99
}

export enum MemoryImportance {
  Low = 0,
  Medium = 1,
  High = 2,
  Critical = 3
}

export interface Memory {
  id: number
  title: string
  content: string
  type: MemoryType
  importance: MemoryImportance
  tags: string[]
  source?: string
  categoryId?: number
  categoryName?: string
  accessCount: number
  lastAccessedAt?: string
  relevanceScore?: number
  createdAt: string
  updatedAt: string
}

export interface MemoryCategory {
  id: number
  name: string
  description?: string
  icon?: string
  sortOrder: number
  memoryCount: number
  createdAt: string
}

export interface CreateMemoryRequest {
  title: string
  content: string
  type?: MemoryType
  importance?: MemoryImportance
  tags?: string[]
  source?: string
  categoryId?: number
}

export interface UpdateMemoryRequest {
  title?: string
  content?: string
  type?: MemoryType
  importance?: MemoryImportance
  tags?: string[]
  source?: string
  categoryId?: number
}

export interface SearchMemoryRequest {
  keyword?: string
  type?: MemoryType
  categoryId?: number
  minImportance?: MemoryImportance
  tag?: string
  page?: number
  pageSize?: number
}

export interface MemorySearchResult {
  items: Memory[]
  total: number
  page: number
  pageSize: number
}

export interface CreateMemoryCategoryRequest {
  name: string
  description?: string
  icon?: string
}

export interface UpdateMemoryCategoryRequest {
  name?: string
  description?: string
  icon?: string
  sortOrder?: number
}

export interface ImportMemoryRequest {
  memories: Array<{
    title: string
    content: string
    type?: number
    importance?: number
    tags?: string[]
    source?: string
    categoryId?: number
  }>
  importMode?: 'skip' | 'overwrite' | 'merge'
}

export interface MemoryStats {
  totalMemories: number
  totalCategories: number
  todayAccessed: number
  weekAccessed: number
  byType: Record<string, number>
  byImportance: Record<string, number>
  recentMemories: Memory[]
  frequentlyAccessed: Memory[]
}

export const memoryTypeLabels: Record<MemoryType, string> = {
  [MemoryType.Fact]: '事实',
  [MemoryType.Preference]: '偏好',
  [MemoryType.Project]: '项目',
  [MemoryType.Personal]: '个人',
  [MemoryType.Workflow]: '工作流',
  [MemoryType.Skill]: '技能',
  [MemoryType.Other]: '其他'
}

export const memoryImportanceLabels: Record<MemoryImportance, string> = {
  [MemoryImportance.Low]: '低',
  [MemoryImportance.Medium]: '中',
  [MemoryImportance.High]: '高',
  [MemoryImportance.Critical]: '关键'
}

export const memoryImportanceColors: Record<MemoryImportance, string> = {
  [MemoryImportance.Low]: '#9ca3af',
  [MemoryImportance.Medium]: '#3b82f6',
  [MemoryImportance.High]: '#f59e0b',
  [MemoryImportance.Critical]: '#ef4444'
}
