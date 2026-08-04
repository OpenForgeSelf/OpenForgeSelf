/**
 * 应用启动时初始化 token。
 * 检查 localStorage 是否有 token，没有则通过后端 init-token 接口获取。
 */
import { getStoredToken, setStoredToken, STORAGE_KEY } from './request';

/** 后端 init-token 接口（无认证，后端首次返回初始密钥） */
const API_BASE = import.meta.env.VITE_API_BASE_URL || '';

async function fetchInitToken(): Promise<string | null> {
  try {
    const res = await fetch(`${API_BASE}/api/api-server/init-token`, {
      headers: { 'Content-Type': 'application/json' },
    });
    if (!res.ok) return null;
    const json = await res.json();
    if (json?.data?.apiKeyPlain) {
      return json.data.apiKeyPlain;
    }
    return null;
  } catch {
    return null;
  }
}

/**
 * 初始化 token。应用启动时调用一次。
 * - 如果 localStorage 已有 token，跳过
 * - 否则调用 /api/api-server/init-token 获取初始 token 并存储
 */
export async function initAuthToken(): Promise<void> {
  if (getStoredToken()) return;

  const token = await fetchInitToken();
  if (token) {
    setStoredToken(token);
    console.log('[Auth] Token 已初始化并存储');
  } else {
    console.warn('[Auth] 获取 token 失败，API 接口无认证时将正常工作');
  }
}
