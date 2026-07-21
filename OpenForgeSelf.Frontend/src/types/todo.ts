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
