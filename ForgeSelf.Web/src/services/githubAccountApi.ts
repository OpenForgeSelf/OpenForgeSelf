/**
 * GitHub 账号绑定 API 层（PILOT-083）。
 * 全部端点为已鉴权控制器（ApiKeyPolicy）→ 一律走 authFetch（铁律 17）。
 * 响应外壳 { success, data | error }：业务失败不抛错，返回 success:false + error。
 */

import { authFetch } from './authFetch'
import type { ApiEnvelope, DeviceCodeStart, GitHubBindingStatus, GitHubPollResult } from '@/types/githubAccount'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

async function readEnvelope<T>(response: Response): Promise<ApiEnvelope<T>> {
  try {
    return (await response.json()) as ApiEnvelope<T>
  } catch {
    return { success: false, error: `响应解析失败（HTTP ${response.status}）` }
  }
}

export const githubAccountApi = {
  /** 绑定状态 */
  async status(): Promise<ApiEnvelope<GitHubBindingStatus>> {
    const response = await authFetch(`${API_BASE_URL}/github-account/status`)
    return readEnvelope<GitHubBindingStatus>(response)
  },

  /** 发起绑定：申请设备码（后端等待期） */
  async startBind(): Promise<ApiEnvelope<DeviceCodeStart>> {
    const response = await authFetch(`${API_BASE_URL}/github-account/bind/start`, { method: 'POST' })
    return readEnvelope<DeviceCodeStart>(response)
  },

  /** 轮询一次绑定结果（pending/slow_down 为协议内等待态） */
  async pollBind(deviceCode: string): Promise<ApiEnvelope<GitHubPollResult>> {
    const response = await authFetch(`${API_BASE_URL}/github-account/bind/poll`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ deviceCode }),
    })
    return readEnvelope<GitHubPollResult>(response)
  },

  /** 解绑 */
  async unbind(): Promise<ApiEnvelope<GitHubBindingStatus>> {
    const response = await authFetch(`${API_BASE_URL}/github-account/bind`, { method: 'DELETE' })
    return readEnvelope<GitHubBindingStatus>(response)
  },

  /** 绑定态开关：插件 GitHub 拉取是否自动附凭据 */
  async setAutoAttach(enabled: boolean): Promise<ApiEnvelope<GitHubBindingStatus>> {
    const response = await authFetch(`${API_BASE_URL}/github-account/auto-attach`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled }),
    })
    return readEnvelope<GitHubBindingStatus>(response)
  },
}
