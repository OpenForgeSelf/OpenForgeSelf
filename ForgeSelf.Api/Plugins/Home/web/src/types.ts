/**
 * 待办追踪类型定义
 */

export type TodoStatus = 'Pending' | 'Completed'

export interface TodoItem {
  id: number
  title: string
  remark?: string | null
  status: TodoStatus
  dueDate?: string | null
  createdAt: string
  updatedAt: string
  completedAt?: string | null
}

export interface TodoPagedResult {
  items: TodoItem[]
  total: number
  page: number
  pageSize: number
}

export interface TodoQueryParams {
  status?: TodoStatus
  page?: number
  pageSize?: number
}

export interface TodoCreateRequest {
  title: string
  remark?: string
  dueDate?: string
}

export interface TodoUpdateRequest {
  title?: string
  remark?: string
  dueDate?: string
}

/** 本地配置（ForgeSetting.config 前端类型，仅含首页需要字段）。 */
export interface AppSettings {
  defaultModel: string
  homePluginId: string
}

/**
 * 由后端前端插件清单归一化出的菜单贡献条目（对应 PluginFrontendManifest.frontend）。
 * 供 features.ts 的 mergeFeatureList 做「内置功能 + 运行期插件」合并。
 */
export interface PluginMenuContribution {
  id: string
  name: string
  menu: string
  route: string | null
  icon: string | null
  views: string[]
}
