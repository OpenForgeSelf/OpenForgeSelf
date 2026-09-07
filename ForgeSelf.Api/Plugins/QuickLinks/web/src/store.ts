/**
 * 快捷链接状态管理 Store（插件自带界面，从宿主 stores/quickLinks 移植）。
 *
 * 保留两处已修复：搜索 filteredLinks 对可空字段做 `?? ''` 兜底（防 null.description 调用
 * toLowerCase 整页崩溃）；以及桌面端侧栏自动展开逻辑（在 QuickLinksView 中）。
 */

import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
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
import { quickLinksApi } from './api'

export const useQuickLinksStore = defineStore('quickLinks', () => {
  const links = ref<QuickLink[]>([])
  const categories = ref<QuickLinkCategory[]>([])
  const currentCategory = ref<string | null>(null)
  const loading = ref(false)
  const searchKeyword = ref('')
  const error = ref<string | null>(null)

  const filteredLinks = computed(() => {
    let result = [...links.value]

    if (currentCategory.value) {
      result = result.filter(link => link.categoryId === currentCategory.value)
    }

    if (searchKeyword.value.trim()) {
      const keyword = searchKeyword.value.toLowerCase()
      result = result.filter(link =>
        (link.name ?? '').toLowerCase().includes(keyword) ||
        (link.description ?? '').toLowerCase().includes(keyword) ||
        (link.url ?? '').toLowerCase().includes(keyword)
      )
    }

    return result
  })

  const sortedLinks = computed(() => {
    return [...filteredLinks.value].sort((a, b) => a.sortOrder - b.sortOrder)
  })

  const sortedCategories = computed(() => {
    return [...categories.value].sort((a, b) => a.sortOrder - b.sortOrder)
  })

  async function loadLinks(params?: QuickLinkQueryParams): Promise<void> {
    try {
      loading.value = true
      error.value = null
      links.value = await quickLinksApi.fetchLinks(params)
    } catch (e) {
      console.error('加载链接列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载链接列表失败'
    } finally {
      loading.value = false
    }
  }

  async function loadCategories(): Promise<void> {
    try {
      error.value = null
      categories.value = await quickLinksApi.fetchCategories()
    } catch (e) {
      console.error('加载分类列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载分类列表失败'
    }
  }

  async function addLink(link: QuickLinkCreateRequest): Promise<QuickLink> {
    try {
      error.value = null
      const newLink = await quickLinksApi.createLink(link)
      links.value.push(newLink)
      return newLink
    } catch (e) {
      console.error('创建链接失败:', e)
      error.value = e instanceof Error ? e.message : '创建链接失败'
      throw e
    }
  }

  async function updateLink(id: string, link: QuickLinkUpdateRequest): Promise<QuickLink> {
    try {
      error.value = null
      const updatedLink = await quickLinksApi.updateLink(id, link)
      const index = links.value.findIndex(l => l.id === id)
      if (index !== -1) {
        links.value[index] = updatedLink
      }
      return updatedLink
    } catch (e) {
      console.error('更新链接失败:', e)
      error.value = e instanceof Error ? e.message : '更新链接失败'
      throw e
    }
  }

  async function removeLink(id: string): Promise<void> {
    try {
      error.value = null
      await quickLinksApi.deleteLink(id)
      links.value = links.value.filter(l => l.id !== id)
    } catch (e) {
      console.error('删除链接失败:', e)
      error.value = e instanceof Error ? e.message : '删除链接失败'
      throw e
    }
  }

  async function reorder(orderedIds: string[]): Promise<void> {
    try {
      error.value = null
      await quickLinksApi.reorderLinks(orderedIds)
      orderedIds.forEach((id, index) => {
        const link = links.value.find(l => l.id === id)
        if (link) {
          link.sortOrder = index
        }
      })
    } catch (e) {
      console.error('重新排序失败:', e)
      error.value = e instanceof Error ? e.message : '重新排序失败'
      throw e
    }
  }

  async function recordClick(id: string): Promise<void> {
    try {
      await quickLinksApi.recordClick(id)
      const link = links.value.find(l => l.id === id)
      if (link) {
        link.clickCount++
      }
    } catch (e) {
      console.error('记录点击失败:', e)
    }
  }

  async function importData(linksData: QuickLinkCreateRequest[], mode: ImportMode): Promise<QuickLink[]> {
    try {
      loading.value = true
      error.value = null
      const importedLinks = await quickLinksApi.importLinks(linksData, mode)
      if (mode === 'replace') {
        links.value = importedLinks
      } else {
        links.value = [...links.value, ...importedLinks]
      }
      return importedLinks
    } catch (e) {
      console.error('导入链接失败:', e)
      error.value = e instanceof Error ? e.message : '导入链接失败'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function exportData(categoryId?: string): Promise<QuickLink[]> {
    try {
      error.value = null
      return await quickLinksApi.exportLinks(categoryId)
    } catch (e) {
      console.error('导出链接失败:', e)
      error.value = e instanceof Error ? e.message : '导出链接失败'
      throw e
    }
  }

  async function addCategory(category: CategoryCreateRequest): Promise<QuickLinkCategory> {
    try {
      error.value = null
      const newCategory = await quickLinksApi.createCategory(category)
      categories.value.push(newCategory)
      return newCategory
    } catch (e) {
      console.error('创建分类失败:', e)
      error.value = e instanceof Error ? e.message : '创建分类失败'
      throw e
    }
  }

  async function updateCategory(id: string, category: CategoryUpdateRequest): Promise<QuickLinkCategory> {
    try {
      error.value = null
      const updatedCategory = await quickLinksApi.updateCategory(id, category)
      const index = categories.value.findIndex(c => c.id === id)
      if (index !== -1) {
        categories.value[index] = updatedCategory
      }
      return updatedCategory
    } catch (e) {
      console.error('更新分类失败:', e)
      error.value = e instanceof Error ? e.message : '更新分类失败'
      throw e
    }
  }

  async function removeCategory(id: string): Promise<void> {
    try {
      error.value = null
      await quickLinksApi.deleteCategory(id)
      categories.value = categories.value.filter(c => c.id !== id)
      if (currentCategory.value === id) {
        currentCategory.value = null
      }
      links.value = links.value.filter(l => l.categoryId !== id)
    } catch (e) {
      console.error('删除分类失败:', e)
      error.value = e instanceof Error ? e.message : '删除分类失败'
      throw e
    }
  }

  function setCurrentCategory(categoryId: string | null): void {
    currentCategory.value = categoryId
  }

  function setSearchKeyword(keyword: string): void {
    searchKeyword.value = keyword
  }

  function clearError(): void {
    error.value = null
  }

  return {
    links,
    categories,
    currentCategory,
    loading,
    searchKeyword,
    error,
    filteredLinks,
    sortedLinks,
    sortedCategories,
    loadLinks,
    loadCategories,
    addLink,
    updateLink,
    removeLink,
    reorder,
    recordClick,
    importData,
    exportData,
    addCategory,
    updateCategory,
    removeCategory,
    setCurrentCategory,
    setSearchKeyword,
    clearError
  }
})
