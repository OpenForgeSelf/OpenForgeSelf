import type { AppSettings, UpdateAppSettings } from '@/types/settings'
import { request } from './request'

const API_BASE = '/api/settings'

/** 后端字段兜底归一（旧配置/旧后端缺字段时取默认值，行为=直连） */
function normalize(data: any): AppSettings {
  const d = data ?? {}
  return {
    defaultModel: d.defaultModel ?? '',
    homePluginId: d.homePluginId ?? 'home',
    proxyEnabled: !!d.proxyEnabled,
    proxyHost: d.proxyHost ?? '',
    proxyPort: typeof d.proxyPort === 'number' ? d.proxyPort : 0,
    proxyUsername: d.proxyUsername ?? '',
    proxyPasswordSet: !!d.proxyPasswordSet,
  }
}

/**
 * 本地配置（ForgeSetting.config）API 封装。
 * 读写均落 XML 配置文件，不落库。
 */
export const settingsApi = {
  /** 获取当前本地配置 */
  async getSettings(): Promise<AppSettings> {
    const json = await request<any>(API_BASE)
    return normalize(json.data)
  },

  /** 更新本地配置（需要鉴权）；缺省字段=不修改，proxyPassword 空串=清空 */
  async updateSettings(settings: UpdateAppSettings): Promise<AppSettings> {
    const json = await request<any>(API_BASE, {
      method: 'POST',
      body: JSON.stringify(settings),
    })
    return normalize(json.data)
  },
}
