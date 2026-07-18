/**
 * 插件状态管理Store
 */

import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  PluginInfo,
  PluginDetail,
  PluginMenuItem,
  PluginToolFunction,
  PluginListParams,
  PluginCategory,
  PluginSearchParams,
  PluginSearchResult,
  PluginVersionInfo,
  PluginUpdateInfo,
  PluginTemplateInfo,
  ScaffoldRequest
} from '@/types/plugin'
import { pluginApi } from '@/services/pluginApi'

export const usePluginStore = defineStore('plugin', () => {
  const plugins = ref<PluginInfo[]>([])
  const currentPlugin = ref<PluginDetail | null>(null)
  const menuItems = ref<PluginMenuItem[]>([])
  const toolFunctions = ref<PluginToolFunction[]>([])
  const categories = ref<PluginCategory[]>([])
  const recommendedPlugins = ref<PluginInfo[]>([])
  const popularPlugins = ref<PluginInfo[]>([])
  const updates = ref<PluginUpdateInfo[]>([])
  const versions = ref<PluginVersionInfo[]>([])
  const templates = ref<PluginTemplateInfo[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const searchResult = ref<PluginSearchResult | null>(null)

  const enabledPlugins = computed(() =>
    plugins.value.filter(p => p.isEnabled)
  )

  const sortedMenuItems = computed(() => {
    const items = [...menuItems.value]
    items.sort((a, b) => a.order - b.order)
    return items
  })

  const hasUpdates = computed(() => updates.value.length > 0)

  async function loadPlugins(params?: PluginListParams): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      plugins.value = await pluginApi.fetchPlugins(params)
    } catch (e) {
      console.error('加载插件列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载插件列表失败'
    } finally {
      isLoading.value = false
    }
  }

  async function loadPluginDetail(pluginId: string): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      currentPlugin.value = await pluginApi.fetchPluginDetail(pluginId)
    } catch (e) {
      console.error('加载插件详情失败:', e)
      error.value = e instanceof Error ? e.message : '加载插件详情失败'
    } finally {
      isLoading.value = false
    }
  }

  async function enablePlugin(pluginId: string): Promise<void> {
    try {
      error.value = null
      await pluginApi.enablePlugin(pluginId)
      const plugin = plugins.value.find(p => p.id === pluginId)
      if (plugin) {
        plugin.isEnabled = true
      }
      if (currentPlugin.value?.id === pluginId) {
        currentPlugin.value.isEnabled = true
      }
    } catch (e) {
      console.error('启用插件失败:', e)
      error.value = e instanceof Error ? e.message : '启用插件失败'
      throw e
    }
  }

  async function disablePlugin(pluginId: string): Promise<void> {
    try {
      error.value = null
      await pluginApi.disablePlugin(pluginId)
      const plugin = plugins.value.find(p => p.id === pluginId)
      if (plugin) {
        plugin.isEnabled = false
      }
      if (currentPlugin.value?.id === pluginId) {
        currentPlugin.value.isEnabled = false
      }
    } catch (e) {
      console.error('禁用插件失败:', e)
      error.value = e instanceof Error ? e.message : '禁用插件失败'
      throw e
    }
  }

  async function loadMenuItems(): Promise<void> {
    try {
      error.value = null
      menuItems.value = await pluginApi.fetchMenuItems()
    } catch (e) {
      console.error('加载菜单项失败:', e)
      error.value = e instanceof Error ? e.message : '加载菜单项失败'
    }
  }

  async function loadToolFunctions(): Promise<void> {
    try {
      error.value = null
      toolFunctions.value = await pluginApi.fetchToolFunctions()
    } catch (e) {
      console.error('加载工具函数失败:', e)
      error.value = e instanceof Error ? e.message : '加载工具函数失败'
    }
  }

  async function loadCategories(): Promise<void> {
    try {
      error.value = null
      categories.value = await pluginApi.fetchCategories()
    } catch (e) {
      console.error('加载分类失败:', e)
      error.value = e instanceof Error ? e.message : '加载分类失败'
    }
  }

  async function searchPlugins(params: PluginSearchParams): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      searchResult.value = await pluginApi.searchPlugins(params)
    } catch (e) {
      console.error('搜索插件失败:', e)
      error.value = e instanceof Error ? e.message : '搜索插件失败'
    } finally {
      isLoading.value = false
    }
  }

  async function loadRecommendedPlugins(limit = 10): Promise<void> {
    try {
      error.value = null
      recommendedPlugins.value = await pluginApi.fetchRecommendedPlugins(limit)
    } catch (e) {
      console.error('加载推荐插件失败:', e)
      error.value = e instanceof Error ? e.message : '加载推荐插件失败'
    }
  }

  async function loadPopularPlugins(limit = 10): Promise<void> {
    try {
      error.value = null
      popularPlugins.value = await pluginApi.fetchPopularPlugins(limit)
    } catch (e) {
      console.error('加载热门插件失败:', e)
      error.value = e instanceof Error ? e.message : '加载热门插件失败'
    }
  }

  async function packagePlugin(pluginId: string): Promise<Blob> {
    try {
      isLoading.value = true
      error.value = null
      return await pluginApi.packagePlugin(pluginId)
    } catch (e) {
      console.error('打包插件失败:', e)
      error.value = e instanceof Error ? e.message : '打包插件失败'
      throw e
    } finally {
      isLoading.value = false
    }
  }

  async function installPlugin(file: File): Promise<PluginDetail> {
    try {
      isLoading.value = true
      error.value = null
      const result = await pluginApi.installPlugin(file)
      await loadPlugins()
      return result
    } catch (e) {
      console.error('安装插件失败:', e)
      error.value = e instanceof Error ? e.message : '安装插件失败'
      throw e
    } finally {
      isLoading.value = false
    }
  }

  async function uninstallPlugin(pluginId: string): Promise<void> {
    try {
      error.value = null
      await pluginApi.uninstallPlugin(pluginId)
      plugins.value = plugins.value.filter(p => p.id !== pluginId)
      if (currentPlugin.value?.id === pluginId) {
        currentPlugin.value = null
      }
    } catch (e) {
      console.error('卸载插件失败:', e)
      error.value = e instanceof Error ? e.message : '卸载插件失败'
      throw e
    }
  }

  async function checkForUpdates(): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      updates.value = await pluginApi.checkForUpdates()
    } catch (e) {
      console.error('检查更新失败:', e)
      error.value = e instanceof Error ? e.message : '检查更新失败'
    } finally {
      isLoading.value = false
    }
  }

  async function updatePlugin(pluginId: string): Promise<void> {
    try {
      error.value = null
      await pluginApi.updatePlugin(pluginId)
      await loadPlugins()
      if (currentPlugin.value?.id === pluginId) {
        await loadPluginDetail(pluginId)
      }
      updates.value = updates.value.filter(u => u.pluginId !== pluginId)
    } catch (e) {
      console.error('更新插件失败:', e)
      error.value = e instanceof Error ? e.message : '更新插件失败'
      throw e
    }
  }

  async function rollbackPlugin(pluginId: string, version: string): Promise<void> {
    try {
      error.value = null
      await pluginApi.rollbackPlugin(pluginId, version)
      await loadPlugins()
      if (currentPlugin.value?.id === pluginId) {
        await loadPluginDetail(pluginId)
      }
    } catch (e) {
      console.error('回滚插件失败:', e)
      error.value = e instanceof Error ? e.message : '回滚插件失败'
      throw e
    }
  }

  async function loadPluginVersions(pluginId: string): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      versions.value = await pluginApi.fetchPluginVersions(pluginId)
    } catch (e) {
      console.error('加载版本历史失败:', e)
      error.value = e instanceof Error ? e.message : '加载版本历史失败'
    } finally {
      isLoading.value = false
    }
  }

  async function loadTemplates(): Promise<void> {
    try {
      error.value = null
      templates.value = await pluginApi.fetchScaffoldTemplates()
    } catch (e) {
      console.error('加载模板列表失败:', e)
      error.value = e instanceof Error ? e.message : '加载模板列表失败'
    }
  }

  async function generateScaffold(request: ScaffoldRequest): Promise<Blob> {
    try {
      isLoading.value = true
      error.value = null
      return await pluginApi.generateScaffold(request)
    } catch (e) {
      console.error('生成脚手架失败:', e)
      error.value = e instanceof Error ? e.message : '生成脚手架失败'
      throw e
    } finally {
      isLoading.value = false
    }
  }

  function clearError(): void {
    error.value = null
  }

  return {
    plugins,
    currentPlugin,
    menuItems,
    toolFunctions,
    categories,
    recommendedPlugins,
    popularPlugins,
    updates,
    versions,
    templates,
    isLoading,
    error,
    searchResult,
    enabledPlugins,
    sortedMenuItems,
    hasUpdates,
    loadPlugins,
    loadPluginDetail,
    enablePlugin,
    disablePlugin,
    loadMenuItems,
    loadToolFunctions,
    loadCategories,
    searchPlugins,
    loadRecommendedPlugins,
    loadPopularPlugins,
    packagePlugin,
    installPlugin,
    uninstallPlugin,
    checkForUpdates,
    updatePlugin,
    rollbackPlugin,
    loadPluginVersions,
    loadTemplates,
    generateScaffold,
    clearError
  }
})
