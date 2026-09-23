/**
 * 插件API服务层 - 封装插件管理相关后端通信
 *
 * 端点前缀铁律：后端 `PluginController` 路由是单数 `api/plugin/...`（`[Route("api/[controller]")]`）。
 * 曾长期误用复数 `/plugins` 导致插件市场全 404（2026-09-22 修复）。
 * 注意：SPA 内部路由（router 的 `/plugins/updates` 等）与插件静态资源 `/plugins/{id}/frontend/**`
 * 与本服务无关，勿混改。
 */

import type {
  PluginInfo,
  PluginDetail,
  PluginMenuItem,
  PluginToolFunction,
  PluginListParams,
  PluginSettings,
  PluginCategory,
  PluginSearchResult,
  PluginSearchParams,
  PluginVersionInfo,
  PluginUpdateInfo,
  PluginTemplateInfo,
  ScaffoldRequest
} from '@/types/plugin'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

interface ApiResponse<T = unknown> {
  data?: T
  message?: string
}

function unwrap<T>(response: Response): Promise<T> {
  return response.json().then((data: ApiResponse<T>) => {
    if (!response.ok) {
      throw new Error(data.message || `请求失败: ${response.status}`)
    }
    return (data.data ?? data) as T
  })
}

export const pluginApi = {
  async fetchPlugins(params?: PluginListParams): Promise<PluginInfo[]> {
    const queryParams = new URLSearchParams()
    if (params?.keyword) {
      queryParams.append('keyword', params.keyword)
    }
    if (params?.isEnabled !== undefined) {
      queryParams.append('isEnabled', String(params.isEnabled))
    }
    if (params?.category) {
      queryParams.append('category', params.category)
    }

    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/plugin${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)
    if (!response.ok) {
      throw new Error(`获取插件列表失败: ${response.status}`)
    }
    return unwrap<PluginInfo[]>(response)
  },

  async fetchPluginDetail(pluginId: string): Promise<PluginDetail> {
    const response = await fetch(`${API_BASE_URL}/plugin/detail/${pluginId}`)
    if (!response.ok) {
      throw new Error(`获取插件详情失败: ${response.status}`)
    }
    return unwrap<PluginDetail>(response)
  },

  async enablePlugin(pluginId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/plugin/${pluginId}/enable`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      }
    })
    if (!response.ok) {
      throw new Error(`启用插件失败: ${response.status}`)
    }
  },

  async disablePlugin(pluginId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/plugin/${pluginId}/disable`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      }
    })
    if (!response.ok) {
      throw new Error(`禁用插件失败: ${response.status}`)
    }
  },

  async fetchPluginSettings(pluginId: string): Promise<PluginSettings> {
    const response = await fetch(`${API_BASE_URL}/plugin/${pluginId}/settings`)
    if (!response.ok) {
      throw new Error(`获取插件设置失败: ${response.status}`)
    }
    return unwrap<PluginSettings>(response)
  },

  async updatePluginSettings(pluginId: string, settings: PluginSettings): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/plugin/${pluginId}/settings`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(settings)
    })
    if (!response.ok) {
      throw new Error(`更新插件设置失败: ${response.status}`)
    }
  },

  async fetchMenuItems(): Promise<PluginMenuItem[]> {
    const response = await fetch(`${API_BASE_URL}/plugin/menu-items`)
    if (!response.ok) {
      throw new Error(`获取菜单项失败: ${response.status}`)
    }
    return unwrap<PluginMenuItem[]>(response)
  },

  async fetchToolFunctions(): Promise<PluginToolFunction[]> {
    const response = await fetch(`${API_BASE_URL}/plugin/tool-functions`)
    if (!response.ok) {
      throw new Error(`获取工具函数失败: ${response.status}`)
    }
    return unwrap<PluginToolFunction[]>(response)
  },

  async fetchCategories(): Promise<PluginCategory[]> {
    const response = await fetch(`${API_BASE_URL}/plugin/categories`)
    if (!response.ok) {
      throw new Error(`获取分类列表失败: ${response.status}`)
    }
    return unwrap<PluginCategory[]>(response)
  },

  async searchPlugins(params: PluginSearchParams): Promise<PluginSearchResult> {
    const queryParams = new URLSearchParams()
    if (params.keyword) queryParams.append('keyword', params.keyword)
    if (params.category) queryParams.append('category', params.category)
    if (params.sortBy) queryParams.append('sortBy', params.sortBy)
    if (params.page) queryParams.append('page', String(params.page))
    if (params.pageSize) queryParams.append('pageSize', String(params.pageSize))

    const queryString = queryParams.toString()
    const response = await fetch(`${API_BASE_URL}/plugin/search${queryString ? `?${queryString}` : ''}`)
    if (!response.ok) {
      throw new Error(`搜索插件失败: ${response.status}`)
    }
    return unwrap<PluginSearchResult>(response)
  },

  async fetchRecommendedPlugins(limit = 10): Promise<PluginInfo[]> {
    const response = await fetch(`${API_BASE_URL}/plugin/recommended?limit=${limit}`)
    if (!response.ok) {
      throw new Error(`获取推荐插件失败: ${response.status}`)
    }
    return unwrap<PluginInfo[]>(response)
  },

  async fetchPopularPlugins(limit = 10): Promise<PluginInfo[]> {
    const response = await fetch(`${API_BASE_URL}/plugin/popular?limit=${limit}`)
    if (!response.ok) {
      throw new Error(`获取热门插件失败: ${response.status}`)
    }
    return unwrap<PluginInfo[]>(response)
  },

  async packagePlugin(pluginId: string): Promise<Blob> {
    const response = await fetch(`${API_BASE_URL}/plugin/package/${pluginId}`, {
      method: 'POST'
    })
    if (!response.ok) {
      throw new Error(`打包插件失败: ${response.status}`)
    }
    return response.blob()
  },

  async installPlugin(file: File): Promise<PluginDetail> {
    const formData = new FormData()
    formData.append('file', file)

    const response = await fetch(`${API_BASE_URL}/plugin/install`, {
      method: 'POST',
      body: formData
    })
    if (!response.ok) {
      throw new Error(`安装插件失败: ${response.status}`)
    }
    return unwrap<PluginDetail>(response)
  },

  async uninstallPlugin(pluginId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/plugin/uninstall/${pluginId}`, {
      method: 'POST'
    })
    if (!response.ok) {
      throw new Error(`卸载插件失败: ${response.status}`)
    }
  },

  async checkForUpdates(): Promise<PluginUpdateInfo[]> {
    const response = await fetch(`${API_BASE_URL}/plugin/updates`)
    if (!response.ok) {
      throw new Error(`检查更新失败: ${response.status}`)
    }
    return unwrap<PluginUpdateInfo[]>(response)
  },

  async updatePlugin(pluginId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/plugin/update/${pluginId}`, {
      method: 'POST'
    })
    if (!response.ok) {
      throw new Error(`更新插件失败: ${response.status}`)
    }
  },

  async rollbackPlugin(pluginId: string, version: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/plugin/rollback/${pluginId}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ version })
    })
    if (!response.ok) {
      throw new Error(`回滚插件失败: ${response.status}`)
    }
  },

  async fetchPluginVersions(pluginId: string): Promise<PluginVersionInfo[]> {
    const response = await fetch(`${API_BASE_URL}/plugin/${pluginId}/versions`)
    if (!response.ok) {
      throw new Error(`获取版本历史失败: ${response.status}`)
    }
    return unwrap<PluginVersionInfo[]>(response)
  },

  async fetchScaffoldTemplates(): Promise<PluginTemplateInfo[]> {
    const response = await fetch(`${API_BASE_URL}/plugin/scaffolder/templates`)
    if (!response.ok) {
      throw new Error(`获取模板列表失败: ${response.status}`)
    }
    return unwrap<PluginTemplateInfo[]>(response)
  },

  async generateScaffold(request: ScaffoldRequest): Promise<Blob> {
    const response = await fetch(`${API_BASE_URL}/plugin/scaffolder/generate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    })
    if (!response.ok) {
      throw new Error(`生成脚手架失败: ${response.status}`)
    }
    return response.blob()
  }
}
