import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  CodeSnippet,
  CodeSnippetListParams,
  CreateCodeSnippetRequest,
  UpdateCodeSnippetRequest
} from '@/types/codeSnippet'
import { codeSnippetApi } from '@/services/codeSnippetApi'

export const useCodeSnippetStore = defineStore('codeSnippet', () => {
  const snippets = ref<CodeSnippet[]>([])
  const currentSnippet = ref<CodeSnippet | null>(null)
  const languages = ref<string[]>([])
  const categories = ref<string[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const totalSnippets = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)
  const searchKeyword = ref('')
  const filterLanguage = ref('')
  const filterCategory = ref('')
  const filterFavorite = ref<boolean | null>(null)

  const favoriteSnippets = computed(() =>
    snippets.value.filter(s => s.isFavorite)
  )

  const recentSnippets = computed(() => {
    const sorted = [...snippets.value].filter(s => s.lastUsedAt)
    sorted.sort((a, b) => {
      const aTime = a.lastUsedAt?.getTime() || 0
      const bTime = b.lastUsedAt?.getTime() || 0
      return bTime - aTime
    })
    return sorted.slice(0, 10)
  })

  async function loadSnippets(params?: CodeSnippetListParams): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      const result = await codeSnippetApi.listSnippets(params)
      snippets.value = result.items
      totalSnippets.value = result.total
      currentPage.value = result.page
      pageSize.value = result.pageSize
    } catch (e) {
      console.error('加载代码片段列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载代码片段列表失败'
    } finally {
      isLoading.value = false
    }
  }

  async function loadSnippet(id: number): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      currentSnippet.value = await codeSnippetApi.getSnippet(id)
    } catch (e) {
      console.error('加载代码片段详情失败:', e)
      error.value = e instanceof Error ? e.message : '加载代码片段详情失败'
    } finally {
      isLoading.value = false
    }
  }

  async function createSnippet(request: CreateCodeSnippetRequest): Promise<CodeSnippet | null> {
    try {
      isLoading.value = true
      error.value = null
      const snippet = await codeSnippetApi.createSnippet(request)
      snippets.value.unshift(snippet)
      totalSnippets.value++
      return snippet
    } catch (e) {
      console.error('创建代码片段失败:', e)
      error.value = e instanceof Error ? e.message : '创建代码片段失败'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function updateSnippet(id: number, request: UpdateCodeSnippetRequest): Promise<CodeSnippet | null> {
    try {
      isLoading.value = true
      error.value = null
      const snippet = await codeSnippetApi.updateSnippet(id, request)
      const index = snippets.value.findIndex(s => s.id === id)
      if (index !== -1) {
        snippets.value[index] = snippet
      }
      if (currentSnippet.value?.id === id) {
        currentSnippet.value = snippet
      }
      return snippet
    } catch (e) {
      console.error('更新代码片段失败:', e)
      error.value = e instanceof Error ? e.message : '更新代码片段失败'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function deleteSnippet(id: number): Promise<boolean> {
    try {
      isLoading.value = true
      error.value = null
      const result = await codeSnippetApi.deleteSnippet(id)
      if (result) {
        snippets.value = snippets.value.filter(s => s.id !== id)
        totalSnippets.value--
        if (currentSnippet.value?.id === id) {
          currentSnippet.value = null
        }
      }
      return result
    } catch (e) {
      console.error('删除代码片段失败:', e)
      error.value = e instanceof Error ? e.message : '删除代码片段失败'
      return false
    } finally {
      isLoading.value = false
    }
  }

  async function favoriteSnippet(id: number, isFavorite: boolean): Promise<boolean> {
    try {
      const result = await codeSnippetApi.favoriteSnippet(id, isFavorite)
      if (result) {
        const snippet = snippets.value.find(s => s.id === id)
        if (snippet) {
          snippet.isFavorite = isFavorite
        }
        if (currentSnippet.value?.id === id) {
          currentSnippet.value.isFavorite = isFavorite
        }
      }
      return result
    } catch (e) {
      console.error('收藏切换失败:', e)
      error.value = e instanceof Error ? e.message : '收藏切换失败'
      return false
    }
  }

  async function incrementUsage(id: number): Promise<void> {
    try {
      await codeSnippetApi.incrementUsage(id)
      const snippet = snippets.value.find(s => s.id === id)
      if (snippet) {
        snippet.usageCount++
        snippet.lastUsedAt = new Date()
      }
      if (currentSnippet.value?.id === id) {
        currentSnippet.value.usageCount++
        currentSnippet.value.lastUsedAt = new Date()
      }
    } catch (e) {
      console.error('增加使用次数失败:', e)
    }
  }

  async function createFromScript(scriptId: number, title?: string): Promise<CodeSnippet | null> {
    try {
      isLoading.value = true
      error.value = null
      const snippet = await codeSnippetApi.createFromScript(scriptId, title)
      snippets.value.unshift(snippet)
      totalSnippets.value++
      return snippet
    } catch (e) {
      console.error('从脚本创建代码片段失败:', e)
      error.value = e instanceof Error ? e.message : '从脚本创建代码片段失败'
      return null
    } finally {
      isLoading.value = false
    }
  }

  async function loadLanguages(): Promise<void> {
    try {
      languages.value = await codeSnippetApi.getLanguages()
    } catch (e) {
      console.error('加载语言列表失败:', e)
    }
  }

  async function loadCategories(): Promise<void> {
    try {
      categories.value = await codeSnippetApi.getCategories()
    } catch (e) {
      console.error('加载分类列表失败:', e)
    }
  }

  function setSearchKeyword(keyword: string) {
    searchKeyword.value = keyword
  }

  function setFilterLanguage(language: string) {
    filterLanguage.value = language
  }

  function setFilterCategory(category: string) {
    filterCategory.value = category
  }

  function setFilterFavorite(isFavorite: boolean | null) {
    filterFavorite.value = isFavorite
  }

  function resetFilters() {
    searchKeyword.value = ''
    filterLanguage.value = ''
    filterCategory.value = ''
    filterFavorite.value = null
  }

  return {
    snippets,
    currentSnippet,
    languages,
    categories,
    isLoading,
    error,
    totalSnippets,
    currentPage,
    pageSize,
    searchKeyword,
    filterLanguage,
    filterCategory,
    filterFavorite,
    favoriteSnippets,
    recentSnippets,
    loadSnippets,
    loadSnippet,
    createSnippet,
    updateSnippet,
    deleteSnippet,
    favoriteSnippet,
    incrementUsage,
    createFromScript,
    loadLanguages,
    loadCategories,
    setSearchKeyword,
    setFilterLanguage,
    setFilterCategory,
    setFilterFavorite,
    resetFilters
  }
})
