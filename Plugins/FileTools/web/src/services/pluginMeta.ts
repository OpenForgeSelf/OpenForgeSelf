import { authFetch } from './authFetch'

/**
 * 插件自身元信息（铁律13：根视图必须显示当前加载的版本号）。
 * 版本取宿主 `GET /api/plugin` 的**已加载**版本，而不是构建期写死的 package.json 版本 ——
 * 走查时要确认的是「现在跑的是哪一版」，构建期常量恰好会在侧载/回滚后说谎。
 */
export async function fetchPluginVersion(pluginId: string): Promise<string> {
  const response = await authFetch('/api/plugin')
  if (!response.ok) return ''

  try {
    const body = (await response.json()) as { data?: Array<{ id: string; version: string }> } | Array<{ id: string; version: string }>
    const list = Array.isArray(body) ? body : (body?.data ?? [])
    return list.find(p => p?.id === pluginId)?.version ?? ''
  } catch {
    return ''
  }
}
