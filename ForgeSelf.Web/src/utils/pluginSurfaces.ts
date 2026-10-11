/**
 * 插件 surface 规范化与消费判定（Plugin-First 架构约定，DESIGN.md）。
 *
 * 与后端 `ForgeSelf.Abstractions.PluginSurfaces` 对齐：
 * - 后端在加载清单时已清洗未知 surface（丢弃并记日志），前端拿到的应已是规范值；
 * - 前端仍做一次防御性归一化，避免手改 plugin.json / 本地 mock 夹具引入脏值。
 *
 * 向后兼容：旧插件未声明 surfaces，但带 route+views，按既有约定视为 `tab`——
 * 这是 dynamicPlugins 注册路由、store.menus 出菜单的事实基线，不能改。
 */
import type { FrontendContributes, PluginSurface } from '@/types/plugin'

/** 全部已知 surface（规范形式，小写连字符）。与后端 PluginSurfaces.All 对齐。 */
export const KNOWN_PLUGIN_SURFACES: readonly PluginSurface[] = [
  'tab',
  'home-widget',
  'quick-action',
  'status-badge',
]

const KNOWN_SET: ReadonlySet<string> = new Set(KNOWN_PLUGIN_SURFACES)

/**
 * 归一化 surface 原始字符串：trim + 小写，未知值丢弃。
 * 保留首次出现的顺序，去重。
 */
export function normalizeSurfaces(raw?: readonly string[] | null): PluginSurface[] {
  if (!raw) return []
  const out: PluginSurface[] = []
  const seen = new Set<PluginSurface>()
  for (const item of raw) {
    if (typeof item !== 'string') continue
    const v = item.trim().toLowerCase()
    if (!KNOWN_SET.has(v)) continue
    const surface = v as PluginSurface
    if (!seen.has(surface)) {
      seen.add(surface)
      out.push(surface)
    }
  }
  return out
}

/**
 * 解析插件实际贡献的 surfaces：
 * 1. 显式声明 `surfaces` 优先（已归一化）；
 * 2. 未声明但带 `route` + 非空 `views` 的旧插件，按既有约定视为 `['tab']`；
 * 3. 否则为空（纯后端插件 / 无界面插件）。
 */
export function resolveSurfaces(frontend: FrontendContributes | null | undefined): PluginSurface[] {
  if (!frontend) return []
  const explicit = normalizeSurfaces(frontend.surfaces)
  if (explicit.length > 0) return explicit
  if (frontend.route && Array.isArray(frontend.views) && frontend.views.length > 0) {
    return ['tab']
  }
  return []
}

/** 判断插件是否声明了指定 surface（按 resolveSurfaces 的归一结果）。 */
export function hasSurface(
  frontend: FrontendContributes | null | undefined,
  kind: PluginSurface,
): boolean {
  return resolveSurfaces(frontend).includes(kind)
}
