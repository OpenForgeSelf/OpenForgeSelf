/**
 * composer 上下文图标行 + 权限三档 + 输入框上方「待你动作」区（041 需求文档 §3）。
 *
 * 为什么独立成文件：这些是**权限语义**（会影响真实工具挂载），必须能被单测逐条断言；
 * 混进 300+ 行的 composer.ts 会让「权限逻辑」淹没在「面板分组」里。
 *
 * 纯逻辑（无 DOM、无网络），组装结果喂给 agent-core 组件的 config 注入。
 */

/* ================= 权限三档 ================= */

/** 权限档位。readonly=只读 / standard=标准 / full=完全访问。 */
export type AccessMode = 'readonly' | 'standard' | 'full'

export interface AccessModeMeta {
  key: AccessMode
  label: string
  desc: string
}

/** 三档定义（图标行胶囊与面板共用这一份，避免文案两处漂移）。 */
export const ACCESS_MODES: AccessModeMeta[] = [
  { key: 'readonly', label: '只读', desc: '只挂读取类工具：不写文件、不执行命令' },
  { key: 'standard', label: '标准', desc: '可写文件与改待办，不执行终端命令' },
  { key: 'full', label: '完全访问', desc: '工具全开，含执行终端命令' },
]

/** 取档位元数据（未知值回退 standard —— 宁可少放开，不可意外放开）。 */
export function accessModeMeta(mode: AccessMode | string | undefined): AccessModeMeta {
  return ACCESS_MODES.find((m) => m.key === mode) ?? ACCESS_MODES[1]
}

/**
 * 「仅完全访问档放开」的工具名（小写比对）—— 第一档风险：能执行任意代码或产生外部副作用。
 *
 * 依据 2026-10-15 实测的 116 个已注册工具（GET /api/ai-agent/chat/tools）逐个判定，非猜测。
 * 判定标准：**执行任意代码 / 控制系统状态 / 跳到外部 / 破坏性删除**。
 *
 * 注意与 `WRITE_TOOLS`（第二档风险，标准档即可）的区别：写文件、改待办属常规操作，
 * 而「跑一条命令」「执行任意代码」不是——后者是本系统能碰到的最大权限面。
 *
 * ⚠ 已知局限（041 §5）：本名单是**人工名册**，漏收即误挂；
 *   根治需 Abstractions 给 AIToolDefinition 补风险元数据（另立项）。
 */
const FULL_ONLY_TOOLS = new Set([
  // 执行任意代码 / 命令
  'run_terminal_command', 'execute_code', 'universal_tool',
  'run_script', 'analyze_script_error', 'suggest_script_fix', 'generate_script',
  'delegate_task', 'execute_workflow', 'sems_run_command',
  // 控制系统状态
  'kill_process',
  // 破坏性删除
  'file_cleanup', 'sems_remove_project', 'sems_delete_command',
  // 跳到外部
  'open_link',
])

/**
 * 「只读档也放开」的工具名：纯计算 / 纯编解码 / 纯格式转换，不碰磁盘与外部状态。
 * 读类工具（read_file / list_* / search_* / get_*）不在此表 —— 它们由默认分支处理。
 */
const READONLY_EXTRA_TOOLS = new Set([
  'calculate', 'text_stats', 'get_current_time', 'convert_timestamp',
  'encode_base64', 'decode_base64', 'base64_encode', 'base64_decode',
  'url_encode', 'url_decode', 'compute_hash', 'compute_hmac',
  'aes_encrypt', 'aes_decrypt', 'format_json', 'format_xml', 'convert_json_yaml',
  'validate_json', 'validate_jwt', 'generate_jwt', 'decode_jwt', 'generate_uuid',
  'generate_snowflake_id', 'generate_qrcode', 'generate_regex', 'test_regex',
  'regex_replace', 'jsonpath_query', 'get_cpu_usage', 'get_memory_usage',
  'get_disk_info', 'get_process_list', 'get_personal_stats', 'get_memory_stats',
  'file_stats', 'folder_stats', 'get_recommendations', 'get_categories',
])

/** 只读档允许的前缀：读 / 查 / 列 / 搜索 / 预览 类命名。 */
const READONLY_PREFIXES = ['read_', 'list_', 'search_', 'get_', 'view_', 'preview_', 'find_', 'query_']

/** 工具名是否属只读。名单外的**默认按可写处理**——只读档宁可少挂，不可误给写能力。 */
function isReadOnlyTool(name: string): boolean {
  const n = String(name ?? '').toLowerCase()
  if (FULL_ONLY_TOOLS.has(n)) return false
  if (READONLY_EXTRA_TOOLS.has(n)) return true
  return READONLY_PREFIXES.some((p) => n.startsWith(p))
}

/** 某工具在某档位下是否允许挂载。 */
export function toolAllowedInMode(toolName: string, mode: AccessMode | string | undefined): boolean {
  switch (mode) {
    case 'readonly':
      return isReadOnlyTool(toolName)
    case 'full':
      return true
    case 'standard':
    default:
      // 标准档：放行一切，只把最高危那批（命令执行 / 任意代码 / 外部副作用）扣下。
      return !FULL_ONLY_TOOLS.has(String(toolName ?? '').toLowerCase())
  }
}

/**
 * 权限档位 + 用户勾选 → 最终下发给后端的 `EnabledToolNames`。
 *
 * 语义：**档位是上限，勾选是进一步收窄**（取交集）。
 * - 用户没勾选（空）：后端语义是「默认全挂」，此时**必须**下发「按档位允许的全集」，
 *   否则档位形同虚设（空 = 全挂 = 绕过档位）——这是本函数存在的核心理由。
 * - 用户勾选了：只保留「勾选 ∩ 档位允许」。
 *
 * 返回 `null` = 不下发该字段（仅 full 档且未勾选：与后端默认行为一致，省一次无谓过滤）。
 */
export function deriveToolAllowlist(
  mode: AccessMode | string | undefined,
  selected: string[] | undefined,
  allTools: string[] | undefined
): string[] | null {
  const picked = (selected ?? []).filter((n) => !!n)
  if (!picked.length && mode === 'full') return null
  const pool = picked.length ? picked : (allTools ?? []).filter((n) => !!n)
  return pool.filter((n) => toolAllowedInMode(n, mode))
}

/* ================= 图标行 ================= */

export interface ContextBarItem {
  /** 宿主动作键：project / agent / model / tools / skills / more */
  key: string
  /** 按钮图标（emoji，与 chips 同一套视觉语言） */
  icon: string
  /** 无障碍名 + 悬浮提示 */
  label: string
  /** 计数角标：工具/技能多选时为个数；0 不占位 */
  count?: number
  /** 已选态（按钮填充主色） */
  active?: boolean
  /** 当前选中项名（面板标题用） */
  detail?: string
}

/**
 * 组装输入框**下方**的图标行。
 * 与 chips 的关键区别：**入口恒显示**——用户必须能点开去选；
 * chips 是「有值才出现」，这正是上一批「入口不可见、功能像没做」的根因。
 */
export function buildContextBar(state: {
  projectDir: string
  agentName: string
  modelName: string
  toolCount: number
  skillCount: number
  /** 直出上限，超出的收进 +N */
  maxDirect?: number
}): ContextBarItem[] {
  const seg = String(state.projectDir ?? '').replace(/\\/g, '/').split('/').filter(Boolean)
  const projectName = seg.length ? seg[seg.length - 1] : ''
  const items: ContextBarItem[] = [
    { key: 'project', icon: '📁', label: '项目', active: !!projectName, detail: projectName },
    { key: 'agent', icon: '🤖', label: 'Agent', active: !!state.agentName, detail: state.agentName },
    { key: 'model', icon: '💠', label: '模型', active: !!state.modelName, detail: state.modelName },
    { key: 'tools', icon: '🔧', label: '工具', count: state.toolCount, active: state.toolCount > 0 },
    { key: 'skills', icon: '⚡', label: '技能', count: state.skillCount, active: state.skillCount > 0 },
  ]
  const max = state.maxDirect ?? items.length
  if (items.length > max) {
    const rest = items.slice(max)
    return [...items.slice(0, max), { key: 'more', icon: '⋯', label: `更多上下文（${rest.length}）` }]
  }
  return items
}

/* ================= 输入框上方「待你动作」区 ================= */

export interface AskItem {
  id: string
  kind: 'approval' | 'question' | 'todo'
  title: string
  detail?: string
  /** kind=approval：风险档 */
  risk?: 'low' | 'medium' | 'high' | 'critical'
  /** kind=question：候选项 */
  options?: string[]
  /** kind=todo：任务清单 */
  todos?: Array<{ id?: string; title: string; done?: boolean }>
  /** kind=todo：完成比例 0~1 */
  progress?: number
}

export interface AskPanel {
  items: AskItem[]
  approvalCount: number
  questionCount: number
  todoCount: number
  todoProgress: number
}

/**
 * 组装输入框上方的「待你动作」区。
 * 只保留**未解决**条目：已批准 / 已回答 / 已完成的不该继续占着输入框上沿。
 * 纯数据组装（过滤 + 计数 + 待办进度），不碰渲染。
 */
export function buildAskPanel(items: AskItem[] | null | undefined): AskPanel {
  const open = (items ?? []).filter((i) => !!i && !!i.id && !!i.title)
  const todoItems = open.filter((i) => i.kind === 'todo')
  // 进度：优先用条目自带的 progress；否则按 todo 清单里 done 的比例算。
  let done = 0
  let total = 0
  for (const t of todoItems) {
    for (const one of t.todos ?? []) {
      total += 1
      if (one.done) done += 1
    }
  }
  const progress = total ? done / total : 0
  return {
    items: open,
    approvalCount: open.filter((i) => i.kind === 'approval').length,
    questionCount: open.filter((i) => i.kind === 'question').length,
    todoCount: todoItems.length,
    todoProgress: progress,
  }
}
