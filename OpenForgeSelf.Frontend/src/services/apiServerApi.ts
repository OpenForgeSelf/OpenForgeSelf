import type { ApiServerConfig } from '@/types/apiServer'

const API_BASE = '/api/api-server'

/**
 * API 服务器管理 API 封装。
 * 提供状态查询与密钥轮换。
 */
export const apiServerApi = {
  /** 获取 API 服务器配置（API 地址、掩码密钥、授权标头） */
  async getConfig(): Promise<ApiServerConfig> {
    const res = await fetch(`${API_BASE}/status`)
    if (!res.ok) throw new Error(`获取 API 服务器状态失败: ${res.status}`)
    const json = await res.json()
    return json.data as ApiServerConfig
  },

  /** 重新生成 API 密钥（旧密钥立即失效） */
  async regenerateKey(): Promise<ApiServerConfig> {
    const res = await fetch(`${API_BASE}/regenerate`, { method: 'POST' })
    if (!res.ok) throw new Error(`密钥重新生成失败: ${res.status}`)
    const json = await res.json()
    return json.data as ApiServerConfig
  },
}
