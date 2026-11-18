/**
 * 项目工程插件的识别与展示规则（需求 v1.0 F19–F21）。
 * 纯函数，不依赖 Vue / fetch：后端扫描 package.json 后也可在前端补分组与搜索。
 */

export type EngineeringPluginGroup = '构建' | '转译' | '代码质量' | '框架' | '其他'

export interface EngineeringPlugin {
  name: string
  version: string
  group: EngineeringPluginGroup
  source: string
}

/** 将包名归入工程插件分组；配置包与未命中规则的包返回 null。 */
export function classifyEngineeringPlugin(name: string): EngineeringPluginGroup | null {
  const n = name.trim()
  if (!n) return null
  if (n.startsWith('eslint-config-') || /^@[^/]+\/eslint-config(-|$)/.test(n)) return null

  if (
    n.startsWith('eslint-plugin-') ||
    /^@[^/]+\/eslint-plugin(-|$)/.test(n)
  ) return '代码质量'
  if (n.startsWith('prettier-plugin-') || /^@[^/]+\/prettier-plugin(-|$)/.test(n)) return '代码质量'

  if (n.startsWith('babel-plugin-') || n.startsWith('@babel/plugin-')) return '转译'

  if (n.startsWith('@vue/cli-plugin-') || n.startsWith('vue-cli-plugin-')) return '框架'

  if (
    n.startsWith('vite-plugin-') ||
    n.endsWith('-vite-plugin') ||
    n.startsWith('@vitejs/plugin-')
  ) return '构建'
  if (n.startsWith('rollup-plugin-') || n.startsWith('unplugin-')) return '构建'
  if (n.endsWith('-webpack-plugin') || n.startsWith('webpack-plugin-')) return '构建'

  return null
}

export function normalizeEngineeringPlugin(raw: Record<string, unknown>): EngineeringPlugin | null {
  const name = String(raw.name ?? raw.package ?? raw.id ?? '').trim()
  if (!name) return null
  const classified = classifyEngineeringPlugin(name)
  if (!classified && looksLikeSemsOrMcpTool(name)) return null
  const groupRaw = String(raw.group ?? raw.category ?? '')
  const group = isGroup(groupRaw) ? groupRaw : (classified ?? '其他')
  if (!classified && group === '其他') return null
  return {
    name,
    version: String(raw.version ?? raw.declaredVersion ?? raw.wanted ?? ''),
    group,
    source: String(raw.source ?? 'package.json') || 'package.json',
  }
}

function isGroup(v: string): v is EngineeringPluginGroup {
  return v === '构建' || v === '转译' || v === '代码质量' || v === '框架' || v === '其他'
}

export function looksLikeSemsOrMcpTool(name: string): boolean {
  return /^sems_/i.test(name) || /^mcp_/i.test(name)
}

export function filterEngineeringPlugins(
  items: EngineeringPlugin[],
  query: string,
): EngineeringPlugin[] {
  const q = query.trim().toLowerCase()
  if (!q) return items
  return items.filter((it) =>
    it.name.toLowerCase().includes(q) || it.group.toLowerCase().includes(q) || it.version.toLowerCase().includes(q),
  )
}

/** 从后端响应里取出插件列表，忽略任何 sems_* / MCP 工具名。 */
export function parseEngineeringPluginsPayload(payload: unknown): {
  plugins: EngineeringPlugin[]
  error: string
} {
  if (payload == null) return { plugins: [], error: '' }
  const rec = payload as Record<string, unknown>
  const error = String(rec.error ?? rec.reason ?? rec.message ?? '')
  const rawList = rec.plugins ?? rec.items ?? rec.tools
  const list = Array.isArray(rawList) ? rawList : []
  const plugins: EngineeringPlugin[] = []
  for (const row of list) {
    if (!row || typeof row !== 'object') continue
    const item = normalizeEngineeringPlugin(row as Record<string, unknown>)
    if (!item) continue
    if (looksLikeSemsOrMcpTool(item.name)) continue
    plugins.push(item)
  }
  return { plugins, error: plugins.length ? '' : error }
}
