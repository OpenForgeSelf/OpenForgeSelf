/**
 * 插件侧带密钥 fetch：与宿主同键（`localStorage['forge_api_token']`），
 * 因此宿主登录后本插件即可直接调用受 `ApiKeyPolicy` 保护的端点。
 * 插件禁止 import 宿主模块（`@/services/authFetch` 在产物里解析不到），故自带这 12 行。
 */
const STORAGE_KEY = 'forge_api_token'

export async function authFetch(url: string, init: RequestInit = {}): Promise<Response> {
  const token = localStorage.getItem(STORAGE_KEY)
  const headers: Record<string, string> = {
    ...(init.headers as Record<string, string> | undefined)
  }
  if (token) headers['Authorization'] = `Bearer ${token}`
  return fetch(url, { ...init, headers })
}
