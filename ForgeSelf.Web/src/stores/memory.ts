import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import {
  MemoryType,
  MemoryImportance,
  type Memory,
  type MemoryCategory,
  type MemoryStats,
  type CreateMemoryRequest,
  type UpdateMemoryRequest,
  type SearchMemoryRequest
} from '@/types/memory'
import { memoryApi } from '@/services/memoryApi'

export const useMemoryStore = defineStore('memory', () => {
  const memories = ref<Memory[]>([])
  const categories = ref<MemoryCategory[]>([])
  const stats = ref<MemoryStats | null>(null)
  const currentCategory = ref<number | null>(null)
  const loading = ref(false)
  const searchKeyword = ref('')
  const filterType = ref<MemoryType | null>(null)
  const filterImportance = ref<MemoryImportance | null>(null)
  const error = ref<string | null>(null)
  const total = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)

  const sortedCategories = computed(() => {
    return [...categories.value].sort((a, b) => a.sortOrder - b.sortOrder)
  })

  const filteredMemories = computed(() => {
    return memories.value
  })

  async function loadMemories(params?: SearchMemoryRequest): Promise<void> {
    try {
      loading.value = true
      error.value = null
      const result = await memoryApi.searchMemories({
        keyword: searchKeyword.value || undefined,
        categoryId: currentCategory.value ?? undefined,
        type: filterType.value ?? undefined,
        minImportance: filterImportance.value ?? undefined,
        page: currentPage.value,
        pageSize: pageSize.value,
        ...params
      })
      memories.value = result.items
      total.value = result.total
    } catch (e) {
      console.error('加载记忆列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载记忆列表失败'
    } finally {
      loading.value = false
    }
  }

  async function loadCategories(): Promise<void> {
    try {
      error.value = null
      categories.value = await memoryApi.getCategories()
    } catch (e) {
      console.error('加载分类列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载分类列表失败'
    }
  }

  async function loadStats(): Promise<void> {
    try {
      error.value = null
      stats.value = await memoryApi.getStats()
    } catch (e) {
      console.error('加载统计数据失败:', e)
      error.value = e instanceof Error ? e.message : '加载统计数据失败'
    }
  }

  async function addMemory(request: CreateMemoryRequest): Promise<Memory> {
    try {
      error.value = null
      const memory = await memoryApi.createMemory(request)
      memories.value.unshift(memory)
      total.value++
      return memory
    } catch (e) {
      console.error('创建记忆失败:', e)
      error.value = e instanceof Error ? e.message : '创建记忆失败'
      throw e
    }
  }

  async function updateMemory(id: number, request: UpdateMemoryRequest): Promise<Memory> {
    try {
      error.value = null
      const updated = await memoryApi.updateMemory(id, request)
      const index = memories.value.findIndex(m => m.id === id)
      if (index !== -1) {
        memories.value[index] = updated
      }
      return updated
    } catch (e) {
      console.error('更新记忆失败:', e)
      error.value = e instanceof Error ? e.message : '更新记忆失败'
      throw e
    }
  }

  async function removeMemory(id: number): Promise<void> {
    try {
      error.value = null
      await memoryApi.deleteMemory(id)
      memories.value = memories.value.filter(m => m.id !== id)
      total.value--
    } catch (e) {
      console.error('删除记忆失败:', e)
      error.value = e instanceof Error ? e.message : '删除记忆失败'
      throw e
    }
  }

  async function addCategory(name: string, description?: string, icon?: string): Promise<MemoryCategory> {
    try {
      error.value = null
      const category = await memoryApi.createCategory({ name, description, icon })
      categories.value.push(category)
      return category
    } catch (e) {
      console.error('创建分类失败:', e)
      error.value = e instanceof Error ? e.message : '创建分类失败'
      throw e
    }
  }

  async function updateCategory(id: number, name?: string, description?: string, icon?: string): Promise<MemoryCategory> {
    try {
      error.value = null
      const updated = await memoryApi.updateCategory(id, { name, description, icon })
      const index = categories.value.findIndex(c => c.id === id)
      if (index !== -1) {
        categories.value[index] = updated
      }
      return updated
    } catch (e) {
      console.error('更新分类失败:', e)
      error.value = e instanceof Error ? e.message : '更新分类失败'
      throw e
    }
  }

  async function removeCategory(id: number): Promise<void> {
    try {
      error.value = null
      await memoryApi.deleteCategory(id)
      categories.value = categories.value.filter(c => c.id !== id)
      if (currentCategory.value === id) {
        currentCategory.value = null
      }
    } catch (e) {
      console.error('删除分类失败:', e)
      error.value = e instanceof Error ? e.message : '删除分类失败'
      throw e
    }
  }

  async function getRelevantMemories(query: string, limit = 5): Promise<Memory[]> {
    try {
      return await memoryApi.getRelevantMemories(query, limit)
    } catch (e) {
      console.error('获取相关记忆失败:', e)
      return []
    }
  }

  function setCurrentCategory(categoryId: number | null): void {
    currentCategory.value = categoryId
    currentPage.value = 1
  }

  function setSearchKeyword(keyword: string): void {
    searchKeyword.value = keyword
    currentPage.value = 1
  }

  function setFilterType(type: MemoryType | null): void {
    filterType.value = type
    currentPage.value = 1
  }

  function setFilterImportance(importance: MemoryImportance | null): void {
    filterImportance.value = importance
    currentPage.value = 1
  }

  function setPage(page: number): void {
    currentPage.value = page
  }

  function clearError(): void {
    error.value = null
  }

  return {
    memories,
    categories,
    stats,
    currentCategory,
    loading,
    searchKeyword,
    filterType,
    filterImportance,
    error,
    total,
    currentPage,
    pageSize,
    sortedCategories,
    filteredMemories,
    loadMemories,
    loadCategories,
    loadStats,
    addMemory,
    updateMemory,
    removeMemory,
    addCategory,
    updateCategory,
    removeCategory,
    getRelevantMemories,
    setCurrentCategory,
    setSearchKeyword,
    setFilterType,
    setFilterImportance,
    setPage,
    clearError
  }
})
