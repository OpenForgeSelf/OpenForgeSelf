/**
 * 插件前端清单 API 服务层 — 拉取后端前端插件清单（contributes 协议）。
 * 供 pluginManifest store 使用，让前端菜单/视图由「已装载插件清单」驱动。
 */

import { authFetch } from './authFetch'

import type { PluginFrontendManifest } from '@/types/plugin'

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

export const pluginManifestApi = {
  async fetchFrontendManifest(): Promise<PluginFrontendManifest[]> {
    const response = await authFetch(`${API_BASE_URL}/plugin/frontend-manifest`)
    if (!response.ok) {
      throw new Error(`获取前端插件清单失败: ${response.status}`)
    }
    return unwrap<PluginFrontendManifest[]>(response)
  }
}
