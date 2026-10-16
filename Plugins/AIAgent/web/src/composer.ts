/**
 * AIAgent composer 的宿主侧组装层：把插件的数据源（工具 / 技能 / 脚本 / Agent / 模型 / 项目文件）
 * 翻译成组件认识的形状（触发分组 + 上下文 chips）。
 *
 * 放在这里而不是组件里，是为了守住 v1.8.0 迁移定的边界：
 * **组件只认 config 注入的 `{ key,title,items }` 与回调，不认识任何 AIAgent 概念。**
 * 本文件是纯逻辑（无 DOM、无网络），可直接在 vitest 里断言。
 */
import { filterItems } from './agent-core/utils/trigger'

/** 一条候选项。kind 决定「选中后干什么」，由 pickItem 分派。 */
export interface ComposerItem {
  id: string
  name: string
  desc?: string
  meta?: string
  kind: 'builtin' | 'tool' | 'skill' | 'script' | 'agent' | 'model' | 'file'
}

export interface ComposerGroup {
  key: string
  title: string
  items: ComposerItem[]
}

/** 组装命令面板所需的数据源（全部 getter，保证每次打开都取最新值）。 */
export interface ComposerSources {
  tools: () => Array<{ name?: string; description?: string; pluginId?: string }>
  skills: () => Array<{ id?: string; name?: string; source?: string }>
  scripts: () => Array<{ id?: string; name?: string; description?: string; runtime?: string }>
  agents: () => Array<{ id?: string; name?: string; description?: string }>
  models: () => Array<{ chatModelId?: string; name?: string; providerName?: string }>
}

/** 内置命令：不依赖任何数据源，永远可用。 */
export const BUILTIN_COMMANDS: ComposerItem[] = [
  { id: 'builtin:clear', name: '/clear', desc: '开始一个新会话', kind: 'builtin' },
  { id: 'builtin:export', name: '/export', desc: '把当前会话导出为 Markdown', kind: 'builtin' },
  { id: 'builtin:help', name: '/help', desc: '列出可用命令与快捷键', kind: 'builtin' },
]

/**
 * 安全取数据源。
 * ★ 任一数据源抛错都不能让整个命令面板弹不出来 —— 脚本插件没启用、技能接口 500，
 *   都只该少一个分组，而不是按 `/` 没反应。（单测抓到的真缺陷，不是理论风险。）
 */
function safeList<T>(fn: (() => T[]) | undefined): T[] {
  try {
    const list = fn?.()
    return Array.isArray(list) ? list : []
  } catch {
    return []
  }
}

/** 把各数据源组装成命令面板的分组（空组不返回，免得面板里挂一排空标题）。 */
export function buildCommandGroups(src: ComposerSources): ComposerGroup[] {
  const groups: ComposerGroup[] = []

  groups.push({ key: 'builtin', title: '内置命令', items: BUILTIN_COMMANDS })

  const tools = safeList(src.tools)
    .filter((t) => !!t.name)
    .map<ComposerItem>((t) => ({
      id: `tool:${t.name}`,
      name: t.name as string,
      desc: t.description ?? '',
      meta: t.pluginId ?? '',
      kind: 'tool',
    }))
  if (tools.length) groups.push({ key: 'tool', title: '工具（加入本会话）', items: tools })

  const skills = safeList(src.skills)
    .filter((s) => !!s.id)
    .map<ComposerItem>((s) => ({
      id: `skill:${s.id}`,
      name: s.name ?? (s.id as string),
      desc: '',
      meta: s.source ?? '',
      kind: 'skill',
    }))
  if (skills.length) groups.push({ key: 'skill', title: '技能（注入提示词）', items: skills })

  const scripts = safeList(src.scripts)
    .filter((s) => !!s.id)
    .map<ComposerItem>((s) => ({
      id: `script:${s.id}`,
      name: s.name ?? (s.id as string),
      desc: s.description ?? '',
      meta: s.runtime ?? '',
      kind: 'script',
    }))
  if (scripts.length) groups.push({ key: 'script', title: '脚本', items: scripts })

  const agents = safeList(src.agents)
    .filter((a) => !!a.id)
    .map<ComposerItem>((a) => ({
      id: `agent:${a.id}`,
      name: a.name ?? (a.id as string),
      desc: a.description ?? '',
      kind: 'agent',
    }))
  if (agents.length) groups.push({ key: 'agent', title: '切换 Agent', items: agents })

  const models = safeList(src.models)
    .filter((m) => !!m.chatModelId)
    .map<ComposerItem>((m) => ({
      id: `model:${m.chatModelId}`,
      name: m.name ?? (m.chatModelId as string),
      desc: '',
      meta: m.providerName ?? '',
      kind: 'model',
    }))
  if (models.length) groups.push({ key: 'model', title: '切换模型', items: models })

  return groups
}

/** 默认每组展示上限（112 个工具全铺开会直接盖住输入框）。 */
export const GROUP_LIMIT = 50

/** 命令面板的过滤入口：组内过滤 + 截断 + 丢弃空组。 */
export function filterCommandGroups(groups: ComposerGroup[], query: string): ComposerGroup[] {
  return groups
    .map((g) => ({ ...g, items: filterItems(g.items, query, GROUP_LIMIT) }))
    .filter((g) => g.items.length > 0)
}

/**
 * 选中一项后的动作分派。
 *
 * ★ 语义（闸门1 P3 拍板）：工具 / 技能 / Agent / 模型 **挂上下文、不写进输入文本** ——
 *   后端契约本就是 `selectedToolNames` 数组，把工具名当文本塞进消息等于让模型读自然语言。
 *   内置命令与脚本则执行 / 插入文本（脚本插可编辑文本，用户能改参数再发）。
 *
 * @returns 需要插入输入区的文本（'' = 不插，只挂上下文）
 */
export function pickItem(item: ComposerItem, actions: {
  toggleTool: (name: string) => void
  toggleSkill: (id: string) => void
  selectAgent: (id: string) => void
  selectModel: (id: string) => void
  runBuiltin: (id: string) => void
}): string {
  const raw = String(item.id ?? '')
  const value = raw.slice(raw.indexOf(':') + 1)
  switch (item.kind) {
    case 'tool':
      actions.toggleTool(value)
      return ''
    case 'skill':
      actions.toggleSkill(value)
      return ''
    case 'agent':
      actions.selectAgent(value)
      return ''
    case 'model':
      actions.selectModel(value)
      return ''
    case 'builtin':
      actions.runBuiltin(value)
      return ''
    case 'script':
      return `运行脚本 ${item.name} `
    case 'file':
      /* `@` 提及：插入可编辑文本，用户能改成「@src/a.ts 里第 20 行」这类说法 */
      return `@${item.name} `
    default:
      return ''
  }
}

/* ---------------- `@` 文件提及 ---------------- */

/**
 * 文件树遍历要跳过的目录：这些目录里 99% 是噪声，还会让大项目直接卡死。
 * 日志目录、构建产物、缓存、发布产物都算 —— `@` 引用的实用场景永远是
 * 「我记得那个源码文件名」，让 1500 条上限全被 .log 占满等于没有这个功能。
 */
export const FILE_SKIP_DIRS = [
  'node_modules',
  '.git',
  '.claude',
  '.codebuddy',
  'dist',
  'bin',
  'obj',
  '.vs',
  '.idea',
  '.agents',
  '.atomcode',
  '.atomcode-logs',
  '.playwright-mcp',
  '.temp',
  '.vite',
  'artifacts',
  'publish',
  'publish.bak',
  'logs',
]

/** 同样不进入候选的文件扩展名（二进制 / 日志 / 缓存，用户不会去引用它们）。 */
export const FILE_SKIP_EXTS = ['.log', '.dll', '.pdb', '.cache', '.db', '.tmp', '.br', '.gz']

/**
 * 广度优先（BFS）收集项目文件相对路径。
 *
 * ★ 为什么必须是 BFS 而不是 DFS：总量上限 1500 条，DFS 会一头扎进某个深层目录
 *   （比如几千个模板文件）把名额吃光，用户最常用的 `AGENTS.md`、`README.md`、
 *   `src/**` 反而被挤掉。BFS 先把浅层收进来，越常用的越靠前。
 * ★ 单层失败只丢那一层，不让整棵树陪葬。
 *
 * @param listEntries 取某层目录下的条目（宿主提供，实际走 GET /api/project/files）
 * @param maxFiles 总量上限
 * @param maxDepth 最大下钻层数（顶层为 0）
 * @returns 相对路径清单（正斜杠）
 */
export async function collectProjectFiles(
  listEntries: (rel: string) => Promise<Array<{ name: string; isDirectory: boolean }>>,
  maxFiles = MAX_FILES,
  maxDepth = 4
): Promise<string[]> {
  const out: string[] = []
  let frontier: Array<{ rel: string; depth: number }> = [{ rel: '', depth: 0 }]
  while (frontier.length && out.length < maxFiles) {
    const nextLevel: Array<{ rel: string; depth: number }> = []
    for (const { rel, depth } of frontier) {
      if (out.length >= maxFiles) break
      let entries: Array<{ name: string; isDirectory: boolean }> = []
      try {
        entries = (await listEntries(rel)) ?? []
      } catch {
        continue /* 单层失败跳过 */
      }
      for (const it of entries) {
        if (!it.name) continue
        if (it.isDirectory) {
          if (shouldDescend(it.name, depth, maxDepth)) {
            nextLevel.push({ rel: joinRel(rel, it.name), depth: depth + 1 })
          }
        } else if (shouldKeepFile(it.name)) {
          out.push(joinRel(rel, it.name))
          if (out.length >= maxFiles) break
        }
      }
    }
    frontier = nextLevel
  }
  return out
}

/**
 * 判断某层目录是否值得继续下钻。
 * @param depth 当前层级（顶层为 0）
 * @param maxDepth 最多下钻到第几层（默认 4）
 */
export function shouldDescend(name: string, depth: number, maxDepth = 4): boolean {
  if (depth >= maxDepth) return false
  return !FILE_SKIP_DIRS.includes(name)
}

/** 文件是否值得进候选（排除日志 / 二进制 / 缓存等不可能被引用的类型）。 */
export function shouldKeepFile(name: string): boolean {
  const lower = String(name ?? '').toLowerCase()
  return !FILE_SKIP_EXTS.some((ext) => lower.endsWith(ext))
}

/** 把「目录项 + 当前前缀」拼成相对路径（统一分隔符，Windows 后端返回可能带反斜杠）。 */
export function joinRel(prefix: string, name: string): string {
  const p = String(prefix ?? '').replace(/\\/g, '/').replace(/^\/+|\/+$/g, '')
  const n = String(name ?? '').replace(/\\/g, '/')
  return p ? `${p}/${n}` : n
}

/**
 * 文件清单总量上限：超过就不再往下收。
 * 大项目（几万文件）全量塞进面板会让内存过滤与渲染一起变卡，
 * 而 `@` 提及的实用场景永远是「我记得那个文件名」，前 1500 条足够覆盖。
 */
export const MAX_FILES = 1500

/** `@` 提及的候选项过滤：按相对路径匹配，最多 50 条。 */
export function filterFileItems(files: Array<{ path: string; size?: number }>, query: string): Array<{ path: string; size?: number }> {
  const q = String(query ?? '').trim().toLowerCase().replace(/^@/, '')
  const list = (files ?? []).filter((f) => !!f.path)
  const hit = q ? list.filter((f) => f.path.toLowerCase().includes(q)) : list
  return hit.slice(0, GROUP_LIMIT)
}

/* ---------------- 上下文 chips ---------------- */

export interface ContextChip {
  key: string
  label: string
  icon?: string
  removable?: boolean
}

/** 由当前上下文状态生成 chips（有值才出现，空态不占位 —— 与旧版「所见即所发」一致）。 */
export function buildContextChips(state: {
  projectDir: string
  toolNames: string[]
  skillIds: string[]
  skillNames?: Record<string, string>
  agentName: string
  modelName: string
}): ContextChip[] {
  const chips: ContextChip[] = []
  if (state.projectDir) {
    const seg = String(state.projectDir).replace(/\\/g, '/').split('/').filter(Boolean)
    chips.push({ key: 'project', label: seg.length ? seg[seg.length - 1] : state.projectDir, icon: '📁', removable: true })
  }
  if (state.toolNames?.length) {
    chips.push({ key: 'tools', label: `工具 ${state.toolNames.length}`, icon: '🔧', removable: true })
  }
  if (state.skillIds?.length) {
    const first = state.skillNames?.[state.skillIds[0]] ?? state.skillIds[0]
    chips.push({
      key: 'skills',
      label: state.skillIds.length > 1 ? `技能 ${first} +${state.skillIds.length - 1}` : `技能 ${first}`,
      icon: '⚡',
      removable: true,
    })
  }
  if (state.agentName) chips.push({ key: 'agent', label: state.agentName, icon: '🤖', removable: false })
  if (state.modelName) chips.push({ key: 'model', label: state.modelName, icon: '💠', removable: false })
  return chips
}
