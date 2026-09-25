import { STORAGE_KEY } from './request'

/**
 * 统一 fetch：自动附加 API token（Authorization: Bearer <localStorage['forge_api_token']>）。
 * 与 request.ts 同键。⚠ 后端逐控制器 [Authorize("ApiKeyPolicy")]（铁律 17）后，
 * 裸 fetch 不带 token 会 401（曾误判为浏览器环境问题）。凡请求已鉴权控制器，
 * 一律经本函数。注意：跨域/第三方 URL 不得使用（会泄漏 token）。
 */
export async function authFetch(url: string, init: RequestInit = {}): Promise<Response> {
  const token = localStorage.getItem(STORAGE_KEY)
  const headers: Record<string, string> = {
    ...(init.headers as Record<string, string> | undefined),
  }
  if (token) headers['Authorization'] = `Bearer ${token}`
  return fetch(url, { ...init, headers })
}
