/**
 * 深链纯函数（FR14 / AC22 的唯一真源）。
 *
 * 形状：`#/<mode>[/<sub>][?outfit=…&theme=…&device=…]`
 * - `mode ∈ start | showroom | workbench | delivery`（非法整条作废回 null，回落默认规则）；
 * - `sub`：workbench = section key（`shell/nav.ts`），showroom = 页面 id（`showroom/scenes.ts`）；
 *   其它模式不接受 sub；**非法 sub 只丢 sub、保 mode**（不给"猜一个"的机会）；
 * - `outfit/theme/device` 仅对 showroom 有意义：非法值逐项丢弃，同样不影响 mode。
 *
 * 本文件不碰 DOM / 历史：`DesignSystemView.vue` 负责 `history.replaceState` 写回（不新增历史条目），
 * `Showroom` 负责按 `sub/outfit/theme/device` 还原。纯函数化是为了 vitest 直接钉往返一致。
 */
import { isMode, type Mode } from '../shell/mode'
import { isSectionKey } from '../shell/nav'
import { deviceById, findPage } from '../showroom/scenes'

/** 解析后的路由状态（字段缺省 = 未指定） */
export interface RouteState {
  mode: Mode
  /** workbench: section key；showroom: 页面 id */
  sub?: string
  /** 仅 showroom：衣服 id（`preset:<id>` / `project:<code>` / `tuned:<n>`） */
  outfit?: string
  /** 仅 showroom：主题 code */
  theme?: string
  /** 仅 showroom：设备 id */
  device?: string
}

/** 衣服 id 字符集与 `OutfitScope` 的作用域校验一致（`^[a-z0-9:_-]+$`），长度设上限防注入超长串 */
const OUTFIT_RE = /^[a-z0-9:_-]{1,64}$/
/** 主题 code（后端主题 code 形如 `light` / `dark` / `brand-blue`） */
const THEME_RE = /^[A-Za-z0-9_-]{1,32}$/

/**
 * 解析 `location.hash`。非法 mode → `null`；非法 sub / 查询项只丢该项（都保 mode）。
 */
export function parseHash(hash: string): RouteState | null {
  const raw = (hash ?? '').trim()
  const m = /^#\/([a-zA-Z-]+)(?:\/([^?]*))?(?:\?(.*))?$/.exec(raw)
  if (!m) return null
  const mode = m[1]
  if (!isMode(mode)) return null

  const state: RouteState = { mode }

  const sub = (m[2] ?? '').trim()
  if (sub) {
    if (mode === 'workbench' && isSectionKey(sub)) state.sub = sub
    else if (mode === 'showroom' && findPage(sub)) state.sub = sub
    // 其余情况：忽略 sub，不挡 mode
  }

  const params = new URLSearchParams(m[3] ?? '')
  const outfit = params.get('outfit') ?? ''
  if (OUTFIT_RE.test(outfit)) state.outfit = outfit
  const theme = params.get('theme') ?? ''
  if (THEME_RE.test(theme)) state.theme = theme
  const device = params.get('device') ?? ''
  if (deviceById(device)) state.device = device

  return state
}

/**
 * 生成哈希串。查询项顺序固定 `outfit → theme → device`（保证 `formatHash` 结果唯一，可逐字比对）。
 * 值一律原样输出：进入这里的值要么来自 `parseHash`（已按上面正则校验），要么来自应用内部状态。
 */
export function formatHash(state: RouteState): string {
  let out = `#/${state.mode}`
  if (state.sub) out += `/${state.sub}`
  const q: string[] = []
  if (state.outfit) q.push(`outfit=${state.outfit}`)
  if (state.theme) q.push(`theme=${state.theme}`)
  if (state.device) q.push(`device=${state.device}`)
  return q.length ? `${out}?${q.join('&')}` : out
}