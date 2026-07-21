/**
 * 待办 API 服务层 - 封装 /api/todos 端点通信
 */

import type {
  TodoItem,
  TodoPagedResult,
  TodoQueryParams,
  TodoCreateRequest,
  TodoUpdateRequest,
  TodoStatus
} from '@/types/todo'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'
const TODOS_BASE = `${API_BASE_URL}/todos`

/**
 * 解析后端 ApiResponse<T> 包装，返回 T。
 * 兼容直接返回 T 的场景（向后兼容）。
 */
async function parseResponse<T>(response: Response): Promise<T> {
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

  if (response.status === 204) {
    return undefined as unknown as T
  }

  const json = await response.json()
  // ApiResponse<T> 格式: { code, message, success, data }
  // 若返回体含 data 字段则取 data，否则直接返回（兼容）
  return (json?.data !== undefined ? json.data : json) as T
}

export const todoApi = {
  /**
   * 查询待办列表
   */
  async fetchTodos(params?: TodoQueryParams): Promise<TodoPagedResult> {
    const queryParams = new URLSearchParams()
    if (params?.status) {
      queryParams.append('status', params.status)
    }
    if (params?.page !== undefined) {
      queryParams.append('page', String(params.page))
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize))
    }

    const queryString = queryParams.toString()
    const url = `${TODOS_BASE}${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)
    return parseResponse<TodoPagedResult>(response)
  },

  /**
   * 获取单个待办
   */
  async fetchTodoById(id: number): Promise<TodoItem> {
    const response = await fetch(`${TODOS_BASE}/${id}`)
    return parseResponse<TodoItem>(response)
  },

  /**
   * 创建待办
   */
  async createTodo(payload: TodoCreateRequest): Promise<TodoItem> {
    const response = await fetch(TODOS_BASE, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
    return parseResponse<TodoItem>(response)
  },

  /**
   * 更新待办
   */
  async updateTodo(id: number, payload: TodoUpdateRequest): Promise<TodoItem> {
    const response = await fetch(`${TODOS_BASE}/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
    return parseResponse<TodoItem>(response)
  },

  /**
   * 删除待办（204 No Content）
   */
  async deleteTodo(id: number): Promise<void> {
    const response = await fetch(`${TODOS_BASE}/${id}`, {
      method: 'DELETE'
    })
    if (!response.ok && response.status !== 204) {
      throw new Error(`删除待办失败: ${response.status}`)
    }
  },

  /**
   * 标记完成
   */
  async completeTodo(id: number): Promise<TodoItem> {
    const response = await fetch(`${TODOS_BASE}/${id}/complete`, {
      method: 'POST'
    })
    return parseResponse<TodoItem>(response)
  },

  /**
   * 重新打开
   */
  async reopenTodo(id: number): Promise<TodoItem> {
    const response = await fetch(`${TODOS_BASE}/${id}/reopen`, {
      method: 'POST'
    })
    return parseResponse<TodoItem>(response)
  }
}

export type { TodoStatus }
