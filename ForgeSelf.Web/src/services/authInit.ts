/**
 * 应用启动时初始化 token。
 * 检查 localStorage 是否有 token，没有则通过后端 init-token 接口获取。
 */
import { getStoredToken, setStoredToken } from './request';

/** 后端 init-token 接口（无认证，后端首次返回初始密钥） */
const API_BASE = import.meta.env.VITE_API_BASE_URL || '';

/** URL fragment 中携带 token 的键名（形如 #token=sk-xxxx） */
const TOKEN_HASH_KEY = 'token=';

/** 合法 token 的最小长度（低于此值视为非法，防误写入脏数据） */
const MIN_TOKEN_LENGTH = 8;

/** 合法 token 的最大长度（高于此值视为非法，URL 也不该这么长） */
const MAX_TOKEN_LENGTH = 200;

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
 * 从 URL fragment 中取出 token= 之后的值。
 * 同时兼容 `#token=xxx` 与 `#/xxx?token=xxx` 两种形态：
 * 先在 fragment 内定位 `token=`，再截断到下一个 `&` 之前。
 * @param hash 带或不带前导 `#` 的 fragment
 * @returns 原始（未解码）token 值；不存在时返回 null
 */
function extractTokenFromHash(hash: string): string | null {
  const fragment = hash.startsWith('#') ? hash.slice(1) : hash;
  const start = fragment.indexOf(TOKEN_HASH_KEY);
  if (start < 0) return null;
  const after = fragment.slice(start + TOKEN_HASH_KEY.length);
  const end = after.indexOf('&');
  const raw = end >= 0 ? after.slice(0, end) : after;
  return raw.length > 0 ? raw : null;
}

/**
 * 判断 token 格式是否可接受（Q5：直接覆盖本地 token，不做二次确认）。
 * 只做长度与非空校验——真正的合法性由后端认证判定，前端不重复实现业务规则。
 */
function isTokenWellFormed(token: string): boolean {
  return token.length >= MIN_TOKEN_LENGTH && token.length <= MAX_TOKEN_LENGTH;
}

/**
 * 消费 URL fragment 中携带的一次性 token（托盘「打开主界面」入口）。
 *
 * 托盘会以 `http://localhost:{port}/#token=xxx` 拉起浏览器，fragment 不会发往服务器，
 * 也不会进入服务端访问日志；前端在挂载前把它落到 localStorage，随后立即清掉 fragment，
 * 避免 token 残留在地址栏与浏览器历史里。
 *
 * 必须**同步**执行且早于 `initAuthToken()` 与 `app.mount`：
 * 写入后 `initAuthToken()` 会因 `getStoredToken()` 有值而自然跳过，
 * 首屏请求也能带上 Authorization 头。
 *
 * @returns 是否成功消费到 token（true=已写入 localStorage）
 */
export function consumeTokenFromHash(): boolean {
  let consumed = false;

  try {
    const raw = extractTokenFromHash(window.location.hash || '');
    if (raw) {
      // decodeURIComponent 遇到非法转义串（如孤立的 %）会抛错，按「未消费」处理
      const token = decodeURIComponent(raw);
      if (isTokenWellFormed(token)) {
        setStoredToken(token);
        consumed = true;
      } else {
        console.warn('[Auth] URL 中的 token 格式非法，已忽略');
      }
    }
  } catch (e) {
    consumed = false;
    console.warn('[Auth] 解析 URL token 失败：', e);
  }

  // 无论成功还是失败都要清掉 fragment：非法 token 同样不该留在地址栏/历史里
  try {
    window.history.replaceState(
      null,
      '',
      window.location.pathname + window.location.search,
    );
  } catch {
    // 极少数环境（如 file:// 下）replaceState 可能受限，清不掉也不影响主流程
  }

  return consumed;
}

/**
 * 监听 fragment 变化，兜住「同一浏览器已打开主界面时托盘再次打开」这条路径。
 *
 * 该场景下浏览器只改变 fragment、不重新加载文档，`consumeTokenFromHash()` 的首屏调用不会执行，
 * 于是自动认证失效。此处补上监听：一旦消费到 token 就整页刷新，让各视图用新凭据重新取数。
 * （`consumeTokenFromHash` 内部用 `replaceState` 清 fragment，不会再次触发 hashchange，无死循环。）
 *
 * @param reload 刷新页面的动作，默认 `window.location.reload`；抽成参数便于单测注入假实现。
 */
export function installTokenHashWatcher(reload: () => void = () => window.location.reload()): void {
  window.addEventListener('hashchange', () => {
    if (consumeTokenFromHash()) reload();
  });
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
