/**
 * 界面用的纯函数（无 DOM、无网络、可在 vitest 里直接断言）。
 *
 * 收录标准：只放"展示层的换算"。任何涉及**设计值计算**（色彩、对比度、尺度）的一律留在 C#，
 * 前端拿到什么就显示什么——否则又会变成 v1 那种"两套实现各说各话"。
 */
import type { EffectiveToken, Token } from '../api'

/** 与后端 ExportService.CssVarName 同规则：`semantic.text-1` → `--ds-semantic-text-1` */
export function cssVarName(path: string): string {
  return '--ds-' + path.split('.').join('-')
}

/**
 * 层级序读 `GET /meta.tiers`（后端 `TokenTiers.All` 是唯一真源），界面不再自己列一份。
 * 词表外的层级（后端还没认得的取值）排到最后：没证据的顺序不编造。
 */
export function tierRank(tier: string, tiers: readonly string[]): number {
  const i = tiers.indexOf(tier)
  return i < 0 ? tiers.length : i
}

/** 按第一段分组（color / space / size / semantic / component / shadow …），组内按路径字典序 */
export function groupByRoot(items: { path: string }[]): { root: string; items: { path: string }[] }[] {
  const map = new Map<string, { path: string }[]>()
  for (const it of items) {
    const root = it.path.split('.')[0] ?? it.path
    const bucket = map.get(root)
    if (bucket) bucket.push(it)
    else map.set(root, [it])
  }
  return [...map.entries()]
    .map(([root, list]) => ({ root, items: list.sort((a, b) => (a.path < b.path ? -1 : 1)) }))
    .sort((a, b) => (a.root < b.root ? -1 : 1))
}

/** 同一色族的档位序列（brand.50 / brand.100 …），用于色阶条带渲染 */
export function rampSteps(items: EffectiveToken[], family: string): EffectiveToken[] {
  return items
    .filter((t) => t.path.startsWith(`color.${family}.`))
    .sort((a, b) => stepOf(a.path) - stepOf(b.path))
}

export function stepOf(path: string): number {
  const seg = path.split('.').pop() ?? ''
  // 只有**整段是数字**才算数值档：`radius.2xl` 的 "2" 不是档位号，
  // 用 parseInt 会把它读成 2，于是 2xl 被插到 xs/sm 之间（或把 pill/full 顶到它前面）。
  return /^\d+$/.test(seg) ? Number.parseInt(seg, 10) : 0
}

/** `radius.xs` → `xs`（第一段之后的全部，用作档名） */
export function suffixOf(path: string): string {
  return path.slice(path.indexOf('.') + 1)
}

/**
 * 同一前缀下的档位比较器：数值档（`space.4` / `breakpoint.2`）按数值，命名档（`radius.xs` / `duration.micro`）
 * 按**后端词表**（`GET /meta` 的 `scaleOrders`），词表外的排到最后按路径字典序。
 * 词表不在前端另列 —— 后端 `ScaleGenerators` 改档序时，界面必须跟着变，否则又是一份会漂移的镜像。
 */
export function compareSteps(a: string, b: string, order: readonly string[]): number {
  const an = stepOf(a)
  const bn = stepOf(b)
  if (an !== bn) return an - bn
  const ia = order.indexOf(suffixOf(a))
  const ib = order.indexOf(suffixOf(b))
  const ra = ia < 0 ? Number.MAX_SAFE_INTEGER : ia
  const rb = ib < 0 ? Number.MAX_SAFE_INTEGER : ib
  if (ra !== rb) return ra - rb
  return a < b ? -1 : a > b ? 1 : 0
}

/**
 * 从 `meta.variantAxes` 取一条轴的档位序（轴名是契约里的标识，不是词表副本）。
 * 取不到就返回空数组：调用方要么让用户显式自填，要么退回接口原序 —— 不猜没证据的序。
 */
export function axisValues(axes: { axis: string; values: string[] }[], name: string): string[] {
  return axes.find((a) => a.axis === name)?.values ?? []
}

/**
 * 单轴变体的 JSON（键与值都按后端 `CanonicalJson` 的口径拼：一条轴、无多余空白）。
 * 多轴组合仍走 JSON 输入框 —— 界面不发明第二套序列化规则。
 */
export function variantJsonOf(axis: string, value: string): string {
  if (!axis || !value) return '{}'
  const esc = (s: string): string => s.replace(/\\/g, '\\\\').replace(/"/g, '\\"')
  return `{"${esc(axis)}":"${esc(value)}"}`
}

/**
 * 色族清单（brand / accent / neutral / success …）：库里有哪些族由数据决定，
 * **排在前面的由谁**读 `GET /meta.colorFamilies`（后端 `ColorFamilies.All`，与生成器逐族产阶同一张表）。
 * 表外的族（例如用户手工加的 `color.custom.*`）排在表后按字母序 —— 看得见，但不替它编顺序。
 */
export function colorFamilies(items: EffectiveToken[], order: readonly string[]): string[] {
  const set = new Set<string>()
  for (const t of items) {
    const m = /^color\.([a-z0-9-]+)\./.exec(t.path)
    if (m) set.add(m[1])
  }
  return [...set].sort((a, b) => {
    const ia = order.indexOf(a)
    const ib = order.indexOf(b)
    if (ia !== ib) return (ia < 0 ? Number.MAX_SAFE_INTEGER : ia) - (ib < 0 ? Number.MAX_SAFE_INTEGER : ib)
    return a < b ? -1 : a > b ? 1 : 0
  })
}

/**
 * 别名的可读链：`{semantic.brand}` → `semantic.brand`。
 * 界面必须显示"它指向谁"，只显示花括号字符串等于没显示（这是"令牌可解释"的底线）。
 */
export function aliasTarget(token: { aliasPath?: string | null; value?: string | null }): string {
  if (token.aliasPath) return token.aliasPath
  const m = /^\{(.+)\}$/.exec((token.value ?? '').trim())
  return m ? m[1] : ''
}

/** 有效值里混进的 JSON 串（复合令牌），列表视图只给一行摘要，展开才看细节 */
export function valueSummary(token: EffectiveToken): string {
  const raw = token.value ?? ''
  if (!raw.startsWith('{') && !raw.startsWith('[')) return raw
  try {
    const obj = JSON.parse(raw) as Record<string, unknown> | unknown[]
    if (Array.isArray(obj)) return `${obj.length} 层`
    return Object.keys(obj)
      .map((k) => {
        const v = (obj as Record<string, unknown>)[k]
        return `${k}: ${typeof v === 'object' ? JSON.stringify(v) : String(v)}`
      })
      .join(' · ')
  } catch {
    return raw
  }
}

/** 对比度显示：-1 = 后端未计算（不是"1:1"），必须显式说"未测" */
export function ratioText(ratio: number): string {
  if (ratio < 0) return '未测'
  if (ratio >= 21) return '21:1'
  return `${ratio.toFixed(2)}:1`
}

export type WcagLevel = 'fail' | 'large' | 'aa' | 'aaa' | 'unknown'

/** WCAG 2.2 判级（展示用；判定权威在后端 ContrastMath，这里只为着色） */
export function wcagBadge(ratio: number): { level: WcagLevel; label: string } {
  if (ratio < 0) return { level: 'unknown', label: '未测' }
  if (ratio >= 7) return { level: 'aaa', label: 'AAA' }
  if (ratio >= 4.5) return { level: 'aa', label: 'AA' }
  if (ratio >= 3) return { level: 'large', label: '仅大字/图形' }
  return { level: 'fail', label: '不达标' }
}

export function severityClass(severity: string): 'critical' | 'warning' | 'info' {
  return severity === 'critical' ? 'critical' : severity === 'warning' ? 'warning' : 'info'
}

/** 前缀匹配过滤（令牌路径检索够用，不需要模糊算法） */
export function matchesKeyword(path: string, keyword: string): boolean {
  if (!keyword) return true
  const k = keyword.trim().toLowerCase()
  return path.toLowerCase().includes(k)
}

/** 主题轴：色向主题与密度主题分开显示，避免把 compact 当配色切换 */
export function isColorTheme(modeKind: string): boolean {
  return modeKind !== 'density'
}

/** 由令牌行算出"该不该显示人工修改标记" */
export function isHandEdited(token: Token): boolean {
  return (token.generator ?? 'manual') === 'manual'
}

/** 版本号自增建议（界面一键"下一版"）：只动 patch，除非用户手改 */
export function nextVersion(current: string, kind: 'major' | 'minor' | 'patch' = 'patch'): string {
  const parts = (current || '0.0.0').split('.').map((p) => Number.parseInt(p, 10) || 0)
  const [major = 0, minor = 0, patch = 0] = parts
  if (kind === 'major') return `${major + 1}.0.0`
  if (kind === 'minor') return `${major}.${minor + 1}.0`
  return `${major}.${minor}.${patch + 1}`
}
