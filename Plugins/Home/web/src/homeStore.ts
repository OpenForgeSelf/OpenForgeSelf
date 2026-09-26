/**
 * 首页聚合 Store（插件内自备副本）。
 * 汇总首页仪表板所需的各模块状态：AI Agent、工作流、脚本运行器、系统监控、技能、待办。
 * 数据全部经插件 http.ts 直连宿主既有接口（无新后端），无数据时返回空/0/null，不造假。
 *
 * 与宿主 stores/home.ts 的差异：
 * - 不依赖宿主 chat/workflow/scriptRunner/systemMonitor/todo 等 store（插件隔离），改为直连接口。
 * - AI Agent 连接态改用 GET /api/ai-models 探活（插件无 chat WS 实例）；todayConversations 置 0。
 * - 插件菜单贡献经 GET /api/plugin/frontend-manifest 拉取，供首页常用功能合并动态安装的插件。
 */

import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { apiGet } from './http'
import type { TodoItem, TodoCreateRequest, AppSettings, PluginMenuContribution } from './types'

/** 最近活动条目 */
export interface ActivityItem {
  id: string
  type: 'workflow' | 'script' | 'chat' | 'system'
  description: string
  timestamp: Date
}

interface WorkflowLite {
  id: number
  name: string
  status: string
}

interface ScriptLite {
  id: number
  name: string
  lastExecutedAt?: string
}

interface MonitorLite {
  cpu: { totalUsage: number }
  memory: { usagePercent: number }
  disks: { name: string; usagePercent: number }[]
}

export const useHomeStore = defineStore('home', () => {
  // AI Agent 状态（插件无 chat WS，改用 /api/ai-models 探活）
  const aiConnected = ref(false)
  const aiTodayConversations = ref(0)
  const aiCurrentModel = ref('')

  // 工作流状态
  const workflows = ref<WorkflowLite[]>([])
  const workflowStatus = computed(() => ({
    total: workflows.value.length,
    running: workflows.value.filter((w) => w.status === 'running').length,
  }))

  // 脚本运行器状态：从 scripts 列表派生最近执行时间
  const scripts = ref<ScriptLite[]>([])
  const scriptStatus = computed(() => {
    const list = scripts.value
    let lastRunTime: Date | null = null
    for (const s of list) {
      if (!s.lastExecutedAt) continue
      const d = new Date(s.lastExecutedAt)
      if (isNaN(d.getTime())) continue
      if (!lastRunTime || d.getTime() > lastRunTime.getTime()) {
        lastRunTime = d
      }
    }
    return {
      total: list.length,
      lastRunTime,
    }
  })

  // 系统监控（overview 经 mapOverview 归一化）
  const systemMetrics = ref<MonitorLite | null>(null)

  // 今日摘要
  const todayToolCalls = computed(() => aiTodayConversations.value)
  const enabledSkillsCount = ref(0)
  const summaryText = computed(() => {
    if (todayToolCalls.value === 0 && enabledSkillsCount.value === 0) {
      return '开始你的第一次锻造'
    }
    return `今日 ${todayToolCalls.value} 次工具调用 · ${enabledSkillsCount.value} 个技能已启用`
  })

  // 锻层（占位，后续可接入用户画像）
  const forgeLevel = ref(1)
  const forgeProgress = ref(0)

  // 最近活动（暂无真实数据源）
  const recentActivities = ref<ActivityItem[]>([])

  // 待办：派生自 /api/todos 的真实数据，首页只展示最近 5 条待处理项
  const todos = ref<TodoItem[]>([])
  const todoTotal = ref(0)
  const recentTodos = computed<TodoItem[]>(() => todos.value.slice(0, 5))
  const todoPendingTotal = computed(() => todoTotal.value)

  // 插件菜单贡献（供首页常用功能合并动态安装的插件）
  const menuContributions = ref<PluginMenuContribution[]>([])

  /** 后端 overview 字段名与前端结构不一致，统一映射为 HomeView 使用的形态。 */
  function mapOverview(raw: unknown): MonitorLite {
    const d = (raw ?? {}) as Record<string, any>
    const cpu = (d.cpu ?? {}) as Record<string, any>
    const memory = (d.memory ?? {}) as Record<string, any>
    const disks = Array.isArray(d.disks)
      ? (d.disks as Record<string, any>[]).map((disk) => ({
          name: String(disk.driveName ?? disk.name ?? ''),
          usagePercent: Number(disk.usagePercent ?? 0),
        }))
      : []
    return {
      cpu: { totalUsage: Number(cpu.totalUsagePercent ?? cpu.totalUsage ?? 0) },
      memory: { usagePercent: Number(memory.usagePercent ?? 0) },
      disks,
    }
  }

  /**
   * 切换待办完成状态：Pending → Complete / Complete → Reopen，随后刷新列表。
   */
  async function toggleTodo(id: number): Promise<void> {
    const todo = todos.value.find((t) => t.id === id)
    if (!todo) return
    try {
      if (todo.status === 'Pending') {
        await apiGet(`/api/todos/${id}/complete`, { method: 'POST' })
      } else {
        await apiGet(`/api/todos/${id}/reopen`, { method: 'POST' })
      }
      await loadTodos()
    } catch {
      // 错误静默：列表保持原样
    }
  }

  /**
   * 快速创建待办（首页面板入口），随后刷新列表。
   */
  async function addTodo(payload: TodoCreateRequest): Promise<void> {
    try {
      await apiGet('/api/todos', { method: 'POST', body: JSON.stringify(payload) })
      await loadTodos()
    } catch {
      // 错误静默
    }
  }

  /** 拉取待处理待办第一页（pageSize=20，首页取前 5 条展示）。 */
  async function loadTodos(): Promise<void> {
    try {
      const res = await apiGet<{ items?: TodoItem[]; total?: number }>(
        '/api/todos?status=Pending&page=1&pageSize=20',
      )
      todos.value = res?.items ?? []
      todoTotal.value = res?.total ?? todos.value.length
    } catch {
      todos.value = []
      todoTotal.value = 0
    }
  }

  /**
   * 初始化首页聚合数据：并行拉取各模块接口，任一失败保持空/0，不抛错。
   */
  async function init(): Promise<void> {
    const [wf, sc, sk, ov, td, am, cfg, mf] = await Promise.allSettled([
      apiGet<{ items?: WorkflowLite[] }>('/api/workflows?page=1&pageSize=200'),
      apiGet<{ items?: ScriptLite[] }>('/api/scripts?page=1&pageSize=200'),
      apiGet<unknown[]>('/api/skills?isEnabled=true'),
      apiGet<unknown>('/api/monitor/overview'),
      apiGet<{ items?: TodoItem[]; total?: number }>('/api/todos?status=Pending&page=1&pageSize=20'),
      apiGet<unknown>('/api/ai-models'),
      apiGet<AppSettings>('/api/settings'),
      apiGet<{ id: string; name: string; isEnabled: boolean; frontend?: { menu?: string; route?: string | null; icon?: string | null; views?: string[] } }[]>('/api/plugin/frontend-manifest'),
    ])

    if (wf.status === 'fulfilled') workflows.value = wf.value?.items ?? []
    if (sc.status === 'fulfilled') scripts.value = sc.value?.items ?? []
    if (sk.status === 'fulfilled') enabledSkillsCount.value = Array.isArray(sk.value) ? sk.value.length : 0
    if (ov.status === 'fulfilled') systemMetrics.value = mapOverview(ov.value)
    if (td.status === 'fulfilled') {
      todos.value = td.value?.items ?? []
      todoTotal.value = td.value?.total ?? todos.value.length
    }
    if (am.status === 'fulfilled') aiConnected.value = !!am.value
    if (cfg.status === 'fulfilled') aiCurrentModel.value = cfg.value?.defaultModel ?? ''
    if (mf.status === 'fulfilled' && Array.isArray(mf.value)) {
      menuContributions.value = mf.value
        .filter((m) => m.isEnabled && m.frontend && m.frontend.menu)
        .map((m) => ({
          id: m.id,
          name: m.name,
          menu: m.frontend!.menu!,
          route: m.frontend!.route ?? null,
          icon: m.frontend!.icon ?? null,
          views: m.frontend!.views ?? [],
        }))
    }
  }

  return {
    aiAgentStatus: computed(() => ({
      connected: aiConnected.value,
      todayConversations: aiTodayConversations.value,
      currentModel: aiCurrentModel.value,
    })),
    workflowStatus,
    scriptStatus,
    systemMetrics,
    summaryText,
    todayToolCalls,
    enabledSkillsCount,
    forgeLevel,
    forgeProgress,
    recentActivities,
    recentTodos,
    todoPendingTotal,
    menuContributions,
    toggleTodo,
    addTodo,
    init,
  }
})
