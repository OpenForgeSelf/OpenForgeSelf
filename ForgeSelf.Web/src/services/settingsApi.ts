import type { AppSettings, ProxyMode, TestProxyRequest, TestProxyResult, UpdateAppSettings } from '@/types/settings'
import { request } from './request'

const API_BASE = '/api/settings'

const VALID_MODES: ProxyMode[] = ['system', 'manual', 'direct']

/** 后端字段兜底归一（旧配置/旧后端缺字段时取默认值，行为=跟随系统） */
function normalize(data: any): AppSettings {
  const d = data ?? {}
  const mode = String(d.proxyMode ?? 'system').toLowerCase()
  return {
    defaultModel: d.defaultModel ?? '',
    homePluginId: d.homePluginId ?? 'home',
    proxyMode: (VALID_MODES.includes(mode as ProxyMode) ? mode : 'system') as ProxyMode,
    proxyBypass: Array.isArray(d.proxyBypass) ? d.proxyBypass.map((e: unknown) => String(e ?? '')) : [],
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

  /** 更新本地配置（需要鉴权）；缺省字段=不修改，proxyPassword 空串=清空，proxyBypass 空数组=清空 */
  async updateSettings(settings: UpdateAppSettings): Promise<AppSettings> {
    const json = await request<any>(API_BASE, {
      method: 'POST',
      body: JSON.stringify(settings),
    })
    return normalize(json.data)
  },

  /** 测试代理可用性（FR-3/BR-4）：收未保存的表单值，配置零副作用；密码仅本次请求内存使用 */
  async testProxy(req: TestProxyRequest): Promise<TestProxyResult> {
    const json = await request<any>(`${API_BASE}/test-proxy`, {
      method: 'POST',
      body: JSON.stringify(req),
    })
    const d = json.data ?? {}
    return {
      reachable: !!d.reachable,
      statusCode: typeof d.statusCode === 'number' ? d.statusCode : null,
      latencyMs: typeof d.latencyMs === 'number' ? d.latencyMs : 0,
      message: d.message ?? '',
    }
  },
}
