/**
 * 插件前端清单 store — 缓存后端前端插件清单（contributes 协议）。
 * 提供 isEnabled(id) 与 menus getter，供菜单/功能列表渲染时优先读取，
 * 未就绪或失败时由调用方回退到内置 features.ts。
 */

import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type { PluginFrontendManifest, PluginMenuContribution, PluginSurface } from '@/types/plugin'
import { pluginManifestApi } from '@/services/pluginManifestApi'
import { hasSurface, resolveSurfaces } from '@/utils/pluginSurfaces'

export const usePluginManifestStore = defineStore('pluginManifest', () => {
  const manifest = ref<PluginFrontendManifest[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const loaded = ref(false)

  /** 判断指定插件 id 是否已启用（运行中）。 */
  function isEnabled(id: string): boolean {
    return manifest.value.some((m) => m.id === id && m.isEnabled)
  }

  /** 归一化出「已启用且声明了菜单」的贡献条目，供功能列表合并使用。 */
  const menus = computed<PluginMenuContribution[]>(() => {
    const result: PluginMenuContribution[] = []
    for (const m of manifest.value) {
      if (!m.isEnabled) continue
      const frontend = m.frontend
      if (!frontend || !frontend.menu) continue
      result.push({
        id: m.id,
        name: m.name,
        menu: frontend.menu,
        route: frontend.route ?? null,
        icon: frontend.icon ?? null,
        views: frontend.views,
      })
    }
    return result
  })

  /**
   * 已启用插件按 surface 分发的贡献条目（Plugin-First 架构约定）。
   * 每个条目携带插件元信息 + 归一后的 surfaces + frontend 原始声明，
   * 供 Home 面板（home-widget）/ 命令面板（quick-action）/ StatusBar（status-badge）等消费位读取。
   */
  function contributionsBySurface(kind: PluginSurface): PluginFrontendManifest[] {
    return manifest.value.filter((m) => m.isEnabled && hasSurface(m.frontend, kind))
  }

  /** 首页（@forge/home）面板 slot 贡献：hero-composer / function-grid / dashboard-boards / todo / activity-feed 等。 */
  const homeWidgets = computed<PluginFrontendManifest[]>(() => contributionsBySurface('home-widget'))

  /** 全局快捷动作（composer / 命令面板入口）贡献。 */
  const quickActions = computed<PluginFrontendManifest[]>(() => contributionsBySurface('quick-action'))

  /** 状态栏（StatusBar）徽标贡献。 */
  const statusBadges = computed<PluginFrontendManifest[]>(() => contributionsBySurface('status-badge'))

  /** Tab 路由贡献（与 menus 互补：menus 按 menu 文本聚合，这里按 surface 精确过滤）。 */
  const tabRoutes = computed<PluginFrontendManifest[]>(() => contributionsBySurface('tab'))

  /** 便捷方法：取某插件归一后的 surfaces（前端防御性二次归一）。 */
  function surfacesOf(pluginId: string): PluginSurface[] {
    const m = manifest.value.find((x) => x.id === pluginId)
    return m ? resolveSurfaces(m.frontend) : []
  }

  /** 拉取清单；失败时仅记录 error 并保持 loaded=false，由调用方回退内置特性。 */
  async function loadManifest(): Promise<void> {
    try {
      isLoading.value = true
      error.value = null
      manifest.value = await pluginManifestApi.fetchFrontendManifest()
      loaded.value = true
    } catch (e) {
      console.error('加载前端插件清单失败:', e)
      error.value = e instanceof Error ? e.message : '加载前端插件清单失败'
      loaded.value = false
    } finally {
      isLoading.value = false
    }
  }

  return {
    manifest,
    isLoading,
    error,
    loaded,
    isEnabled,
    menus,
    homeWidgets,
    quickActions,
    statusBadges,
    tabRoutes,
    surfacesOf,
    loadManifest,
  }
})
