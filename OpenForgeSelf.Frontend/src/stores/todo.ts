/**
 * 待办追踪 Pinia Store
 */

import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  TodoItem,
  TodoPagedResult,
  TodoQueryParams,
  TodoCreateRequest,
  TodoUpdateRequest,
  TodoStatus
} from '@/types/todo'
import { todoApi } from '@/services/todoApi'

export const useTodoStore = defineStore('todo', () => {
  const items = ref<TodoItem[]>([])
  const total = ref(0)
  const page = ref(1)
  const pageSize = ref(20)
  const statusFilter = ref<TodoStatus | undefined>(undefined)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const pendingItems = computed(() => items.value.filter(t => t.status === 'Pending'))
  const completedItems = computed(() => items.value.filter(t => t.status === 'Completed'))
  const pendingCount = computed(() => pendingItems.value.length)

  async function loadTodos(params?: TodoQueryParams): Promise<void> {
    try {
      loading.value = true
      error.value = null
      const query: TodoQueryParams = {
        status: params?.status ?? statusFilter.value,
        page: params?.page ?? page.value,
        pageSize: params?.pageSize ?? pageSize.value
      }
      const result: TodoPagedResult = await todoApi.fetchTodos(query)
      items.value = result.items
      total.value = result.total
      page.value = result.page
      pageSize.value = result.pageSize
    } catch (e) {
      console.error('加载待办列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载待办列表失败'
    } finally {
      loading.value = false
    }
  }

  async function addTodo(payload: TodoCreateRequest): Promise<TodoItem> {
    try {
      error.value = null
      const created = await todoApi.createTodo(payload)
      items.value.unshift(created)
      total.value += 1
      return created
    } catch (e) {
      console.error('创建待办失败:', e)
      error.value = e instanceof Error ? e.message : '创建待办失败'
      throw e
    }
  }

  async function editTodo(id: number, payload: TodoUpdateRequest): Promise<TodoItem> {
    try {
      error.value = null
      const updated = await todoApi.updateTodo(id, payload)
      const idx = items.value.findIndex(t => t.id === id)
      if (idx !== -1) {
        items.value[idx] = updated
      }
      return updated
    } catch (e) {
      console.error('更新待办失败:', e)
      error.value = e instanceof Error ? e.message : '更新待办失败'
      throw e
    }
  }

  async function removeTodo(id: number): Promise<void> {
    try {
      error.value = null
      await todoApi.deleteTodo(id)
      items.value = items.value.filter(t => t.id !== id)
      total.value = Math.max(0, total.value - 1)
    } catch (e) {
      console.error('删除待办失败:', e)
      error.value = e instanceof Error ? e.message : '删除待办失败'
      throw e
    }
  }

  async function markComplete(id: number): Promise<void> {
    try {
      error.value = null
      const updated = await todoApi.completeTodo(id)
      const idx = items.value.findIndex(t => t.id === id)
      if (idx !== -1) {
        items.value[idx] = updated
      }
    } catch (e) {
      console.error('标记完成失败:', e)
      error.value = e instanceof Error ? e.message : '标记完成失败'
      throw e
    }
  }

  async function markReopen(id: number): Promise<void> {
    try {
      error.value = null
      const updated = await todoApi.reopenTodo(id)
      const idx = items.value.findIndex(t => t.id === id)
      if (idx !== -1) {
        items.value[idx] = updated
      }
    } catch (e) {
      console.error('重新打开失败:', e)
      error.value = e instanceof Error ? e.message : '重新打开失败'
      throw e
    }
  }

  function setStatusFilter(status: TodoStatus | undefined): void {
    statusFilter.value = status
    page.value = 1
  }

  function setPage(p: number): void {
    page.value = p
  }

  function clearError(): void {
    error.value = null
  }

  return {
    items,
    total,
    page,
    pageSize,
    statusFilter,
    loading,
    error,
    pendingItems,
    completedItems,
    pendingCount,
    loadTodos,
    addTodo,
    editTodo,
    removeTodo,
    markComplete,
    markReopen,
    setStatusFilter,
    setPage,
    clearError
  }
})
