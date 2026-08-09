/**
 * 统一 HTTP 请求封装。
 * 自动从 localStorage 读取 token，注入 Authorization 头。
 */
const API_BASE = import.meta.env.VITE_API_BASE_URL || '';

export const STORAGE_KEY = 'forge_api_token';

/** 获取当前存储的 token */
export function getStoredToken(): string | null {
  return localStorage.getItem(STORAGE_KEY);
}

/** 存储 token */
export function setStoredToken(token: string): void {
  localStorage.setItem(STORAGE_KEY, token);
}

/** 清除 token */
export function clearStoredToken(): void {
  localStorage.removeItem(STORAGE_KEY);
}

/** 带 token 的 fetch 封装 */
export async function request<T = any>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const token = getStoredToken();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const url = path.startsWith('http') ? path : `${API_BASE}${path}`;
  const res = await fetch(url, { ...options, headers });
  if (!res.ok) {
    // 优先取后端返回的错误信息（message / title），同时保留状态码便于调用方判断
    let detail = '';
    try {
      const errBody = await res.json();
      detail = errBody?.message || errBody?.title || '';
    } catch {
      // 响应体非 JSON 时忽略
    }
    const reason = detail || res.statusText || '未知错误';
    throw new Error(`请求失败(${res.status}): ${reason}`);
  }
  return res.json();
}
