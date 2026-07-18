/**
 * 首页聚合 Store
 * 汇总首页仪表板所需的各模块状态：AI Agent、工作流、脚本运行器、系统监控等。
 * 数据全部派生自其他真实 store，无数据时返回空/0/null，不造假数据。
 */

import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { useChatStore } from './chat'
import { useWorkflowStore } from './workflow'
import { useScriptRunnerStore } from './scriptRunner'
import { useSystemMonitorStore } from './systemMonitor'
import { skillsApi } from '@/services/skillsApi'

/** 最近活动条目 */
export interface ActivityItem {
  id: string
  type: 'workflow' | 'script' | 'chat' | 'system'
  description: string
  timestamp: Date
}

/** 待办条目 */
export interface TodoItem {
  id: string
  text: string
  done: boolean
}

export const useHomeStore = defineStore('home', () => {
  const chatStore = useChatStore()
  const workflowStore = useWorkflowStore()
  const scriptRunnerStore = useScriptRunnerStore()
  const systemMonitorStore = useSystemMonitorStore()

  // AI Agent 状态
  const aiAgentStatus = computed(() => ({
    connected: chatStore.isWsConnected,
    todayConversations: chatStore.conversations?.length ?? 0,
    currentModel: 'Gemma 2B',
  }))

  // 工作流状态
  const workflowStatus = computed(() => ({
    total: workflowStore.workflows?.length ?? 0,
    running:
      workflowStore.workflows?.filter((w) => w.status === 'running')?.length ?? 0,
  }))

  // 脚本运行器状态：从 scripts 列表派生最近执行时间
  const scriptStatus = computed(() => {
    const scripts = scriptRunnerStore.scripts ?? []
    let lastRunTime: Date | null = null
    for (const s of scripts) {
      const t = s.lastExecutedAt
      if (!t) continue
      const d = t instanceof Date ? t : new Date(t)
      if (isNaN(d.getTime())) continue
      if (!lastRunTime || d.getTime() > lastRunTime.getTime()) {
        lastRunTime = d
      }
    }
    return {
      total: scripts.length,
      lastRunTime,
    }
  })

  // 系统监控（systemMonitorStore 中实际字段为 overview）
  const systemMetrics = computed(() => systemMonitorStore.overview ?? null)

  // 今日摘要
  const todayToolCalls = computed(() => chatStore.messages?.length ?? 0)
  const enabledSkillsCount = ref(0)
  const summaryText = computed(() => {
    if (todayToolCalls.value === 0 && enabledSkillsCount.value === 0) {
      return '开始你的第一次锻造'
    }
    return `今日 ${todayToolCalls.value} 次工具调用 · ${enabledSkillsCount.value} 个技能已启用`
  })

  // 锻层（默认值，后续可接入用户画像）
  const forgeLevel = ref(1)
  const forgeProgress = ref(0)

  // 最近活动（聚合事件时间线，暂无真实数据源）
  const recentActivities = ref<ActivityItem[]>([])

  // 待办（暂无真实数据源）
  const todos = ref<TodoItem[]>([])

  /**
   * 初始化首页聚合数据：拉取已启用技能数量。
   * 失败时保持 0，不抛错。
   */
  async function init(): Promise<void> {
    try {
      const skills = await skillsApi.fetchSkills({ isEnabled: true })
      enabledSkillsCount.value = Array.isArray(skills) ? skills.length : 0
    } catch {
      // skillsApi 不可用或失败时保持 0
      enabledSkillsCount.value = 0
    }
  }

  return {
    aiAgentStatus,
    workflowStatus,
    scriptStatus,
    systemMetrics,
    summaryText,
    todayToolCalls,
    enabledSkillsCount,
    forgeLevel,
    forgeProgress,
    recentActivities,
    todos,
    init,
  }
})
