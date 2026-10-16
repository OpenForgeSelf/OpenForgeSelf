/**
 * AIAgent 宿主页对「上下文图标行 + 权限胶囊 + 待你动作区」的数据组装。
 *
 * 抽成独立文件的原因：这层是**唯一知道 AIAgent 业务概念**的地方（项目/Agent/模型/工具/技能/待办），
 * agent-core 组件与 composerContext 纯逻辑都不该沾。抽出来后 AiAgentView 只剩状态与接线。
 *
 * 全部是纯函数（入参 → config 注入形状），可直接单测；HTTP 由调用方传入 getter。
 */
import {
  ACCESS_MODES,
  buildAskPanel,
  buildContextBar,
  type AskItem,
  type ContextBarItem,
} from './composerContext'

/* ================= 面板行 ================= */

export interface PanelRow {
  id: string
  name: string
  desc?: string
  active?: boolean
}

/** 面板数据源：{ [key]: { title, multi?, items, search(query) } } */
export interface PanelSpec {
  title: string
  /** 多选（工具/技能用复选框；Agent/模型用单选打勾） */
  multi?: boolean
  items: PanelRow[]
  /** 面板内搜索过滤（宿主实现，组件不重复实现） */
  search?: (q: string) => PanelRow[]
  empty?: string
}

/* ================= 图标行 ================= */

export interface ContextBarInputs {
  projectDir: string
  agentName: string
  modelName: string
  tools: Array<{ name?: string; description?: string; pluginId?: string }>
  skills: Array<{ id?: string; name?: string; source?: string }>
  agents: Array<{ id?: string; name?: string; description?: string }>
  models: Array<{ chatModelId?: string; name?: string; providerName?: string }>
  selectedToolNames: string[]
  selectedSkillIds: string[]
  activeAgentId: string
  selectedModelId: string
  accessMode: string
  /** 当前打开的面板 key（用于把搜索词过滤结果塞进 panels[key].visible） */
  openKey?: string | null
  /** 各面板当前搜索词 */
  queries?: Record<string, string>
  /** 项目候选（已知项目目录；为空时面板提示手动输入） */
  projectNames?: string[]
}

/** 项目面板是「自由输入 + 已知目录」，单选语义用 active 表达当前选中。 */
function projectPanel(inputs: ContextBarInputs): PanelSpec {
  const rows: PanelRow[] = (inputs.projectNames ?? []).map((p) => {
    const seg = p.replace(/\\/g, '/').split('/').filter(Boolean)
    return { id: p, name: seg[seg.length - 1] || p, desc: p, active: p === inputs.projectDir }
  })
  /* 清除入口：旧 chips 的「×」有「移除项目」能力，chips 撤掉后不能丢这个功能，
     否则用户选错目录后无从回退（右栏 AI 上下文也没有清除入口）。 */
  if (inputs.projectDir) {
    rows.push({
      id: PROJECT_CLEAR_ID,
      name: '清除当前项目目录',
      desc: '不再限定工作目录，工具改为会话默认作用域',
    })
  }
  return {
    title: '项目目录',
    items: rows,
    empty: '暂无已登记项目，请在右栏「AI 上下文」选择目录',
  }
}

/** 项目面板的「清除」特殊 id —— 不是目录路径，点它走宿主 onClearDirectory。 */
export const PROJECT_CLEAR_ID = '\u0000clear-project'

function agentPanel(inputs: ContextBarInputs): PanelSpec {
  const items = (inputs.agents ?? []).map((a) => ({
    id: a.id ?? '',
    name: a.name ?? a.id ?? '',
    desc: a.description ?? '',
    active: a.id === inputs.activeAgentId,
  }))
  return { title: '选择 Agent', items, empty: '暂无 Agent（先在左栏新建）' }
}

/** 模型展示名：name 为空时折叠 `provider:model` 前缀，别把整串 id 甩给用户。 */
function modelDisplayName(m: { name?: string; chatModelId?: string; providerName?: string }): string {
  const raw = (m.name ?? '').trim()
  if (raw) return raw
  const id = (m.chatModelId ?? '').trim()
  if (!id) return ''
  const tail = id.includes(':') ? id.slice(id.indexOf(':') + 1) : id
  return tail || id
}

function modelPanel(inputs: ContextBarInputs): PanelSpec {
  const items = (inputs.models ?? []).map((m) => ({
    id: m.chatModelId ?? '',
    name: modelDisplayName(m),
    desc: m.providerName ?? '',
    active: m.chatModelId === inputs.selectedModelId,
  }))
  const allDisabled = items.length === 0
  return {
    title: items.length ? `选择模型（${items.length}）` : '选择模型',
    items,
    empty: allDisabled ? '暂无可用模型（检查 AI 提供方配置）' : '',
  }
}

function toolPanel(inputs: ContextBarInputs): PanelSpec {
  const items = (inputs.tools ?? []).map((t) => ({
    id: t.name ?? '',
    name: t.name ?? '',
    desc: t.description ?? t.pluginId ?? '',
    active: inputs.selectedToolNames.includes(t.name ?? ''),
  }))
  return { title: `选择工具（已选 ${inputs.selectedToolNames.length}）`, multi: true, items, empty: '暂无工具' }
}

function skillPanel(inputs: ContextBarInputs): PanelSpec {
  const items = (inputs.skills ?? []).map((s) => ({
    id: s.id ?? '',
    name: s.name ?? s.id ?? '',
    desc: s.source ?? '',
    active: inputs.selectedSkillIds.includes(s.id ?? ''),
  }))
  return { title: `选择技能（已选 ${inputs.selectedSkillIds.length}）`, multi: true, items, empty: '暂无技能' }
}

/** 组装完整 config.contextBar（含各面板的 visible 搜索结果）。 */
export function buildContextBarConfig(inputs: ContextBarInputs): {
  items: ContextBarItem[]
  panels: Record<string, PanelSpec>
  access: { modes: typeof ACCESS_MODES; value: string; onChange?: (m: string) => void }
  onOpen?: (key: string) => void
  onClose?: (key: string) => void
  onPick?: (e: { key: string; id: string }) => void
} {
  const specs: Record<string, PanelSpec> = {
    project: projectPanel(inputs),
    agent: agentPanel(inputs),
    model: modelPanel(inputs),
    tools: toolPanel(inputs),
    skills: skillPanel(inputs),
  }

  /* 搜索过滤：宿主算好塞进 visible（组件只渲染，不重复实现过滤）。
     未打开的面板不预计算，避免每次输入都遍历 116 个工具 × 5 个面板。 */
  const key = inputs.openKey
  const q = key ? (inputs.queries?.[key] ?? '').trim() : ''
  if (key && q) {
    const spec = specs[key]
    if (spec?.search) specs[key] = { ...spec, items: spec.search(q) }
    else if (spec) {
      const lower = q.toLowerCase()
      specs[key] = { ...spec, items: spec.items.filter((r) => `${r.name} ${r.desc ?? ''}`.toLowerCase().includes(lower)) }
    }
  }

  return {
    items: buildContextBar({
      projectDir: inputs.projectDir,
      agentName: inputs.agentName,
      modelName: inputs.modelName,
      toolCount: inputs.selectedToolNames.length,
      skillCount: inputs.selectedSkillIds.length,
    }),
    panels: specs,
    access: { modes: ACCESS_MODES, value: inputs.accessMode },
  }
}

/* ================= 待你动作区 ================= */

export interface TodoDtoLike {
  id?: number | string
  title?: string
  status?: string
  stage?: string
  stageLabel?: string
}

/** TodoTracker DTO → AskItem（只取未完成的；已完成的进清单里划掉，给用户「进度」感知）。 */
export function todosToAskItem(todos: TodoDtoLike[] | null | undefined, limit = 4): AskItem | null {
  const list = (todos ?? []).filter((t) => !!t?.title)
  if (!list.length) return null
  const shown = list.slice(0, limit)
  return {
    id: 'todos',
    kind: 'todo',
    title: `待办 ${list.filter((t) => t.status !== 'Completed').length} 项未完成`,
    todos: shown.map((t) => ({
      id: String(t.id ?? ''),
      title: `${t.stageLabel ?? t.stage ?? ''}${t.stageLabel ? ' · ' : ''}${t.title}`,
      done: t.status === 'Completed',
    })),
  }
}

/** 组装 config.ask（未解决项为空时返回 null，组件不渲染）。 */
export function buildAskConfig(
  items: AskItem[] | null | undefined
): { items: AskItem[]; progress: number } | null {
  const panel = buildAskPanel(items)
  if (!panel.items.length) return null
  return { items: panel.items, progress: panel.todoProgress }
}
