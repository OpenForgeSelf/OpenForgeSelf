import type { AppSettings } from '@/types/settings'
import { request } from './request'

const API_BASE = '/api/settings'

/**
 * 本地配置（ForgeSetting.config）API 封装。
 * 读写均落 XML 配置文件，不落库。
 */
export const settingsApi = {
  /** 获取当前本地配置 */
  async getSettings(): Promise<AppSettings> {
    const json = await request<any>(API_BASE)
    return (json.data ?? { defaultModel: '', homePluginId: 'home' }) as AppSettings
  },

  /** 更新本地配置（需要鉴权） */
  async updateSettings(settings: Partial<AppSettings>): Promise<AppSettings> {
    const json = await request<any>(API_BASE, {
      method: 'POST',
      body: JSON.stringify(settings),
    })
    return (json.data ?? { defaultModel: '', homePluginId: 'home' }) as AppSettings
  },
}
