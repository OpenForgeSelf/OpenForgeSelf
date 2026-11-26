import { defineStore } from 'pinia'
import { ref } from 'vue'
import { blockingCount, classifyRuntime } from '@/services/runtimeBoard'
import type { BoardCard, LaneNotice } from '@/services/runtimeBoard'
import { fetchRuntimeInput, postJson, rescanProjectProcesses } from '@/services/runtimeBoardApi'
import { resumePlannedRun } from '@/services/taskCenterApi'
import { useTabsStore } from './tabs'

const POLL_MS = 15_000

export const useRuntimeBoardStore = defineStore('runtimeBoard', () => {
  const cards = ref<BoardCard[]>([])
  const notices = ref<LaneNotice[]>([])
  const loading = ref(true)
  const loadedOnce = ref(false)
  const scanning = ref(false)
  const scanNote = ref('')
  const results = ref<Record<string, string>>({})
  const acting = ref<Record<string, boolean>>({})
  let timer: number | null = null
  let inflight = false

  function publish(nextCards: BoardCard[], nextNotices: LaneNotice[]): void {
    cards.value = nextCards
    notices.value = nextNotices
    const keep: Record<string, string> = {}
    for (const card of nextCards) {
      if (results.value[card.id]) keep[card.id] = results.value[card.id]
    }
    results.value = keep
    useTabsStore().setBadge('/task-center', blockingCount(nextCards, results.value))
  }

  async function refresh(): Promise<void> {
    if (inflight) return
    inflight = true
    try {
      const input = await fetchRuntimeInput()
      const board = classifyRuntime(input)
      publish(board.cards, board.notices)
      loadedOnce.value = true
    } finally {
      loading.value = false
      inflight = false
    }
  }

  function start(): void {
    if (timer != null) return
    void refresh()
    timer = window.setInterval(() => {
      void refresh()
    }, POLL_MS)
  }

  async function scan(): Promise<void> {
    if (scanning.value) return
    scanning.value = true
    scanNote.value = ''
    try {
      const outcome = await rescanProjectProcesses()
      scanNote.value = outcome.ok ? '已按已登记项目目录查找' : outcome.message
      await refresh()
    } finally {
      scanning.value = false
    }
  }

  async function decide(card: BoardCard, approved: boolean, confirmDestructive = false): Promise<void> {
    if (!card.decide || acting.value[card.id]) return
    acting.value = { ...acting.value, [card.id]: true }
    try {
      const message = await runDecide(card, approved, confirmDestructive)
      results.value = { ...results.value, [card.id]: message }
      useTabsStore().setBadge('/task-center', blockingCount(cards.value, results.value))
    } finally {
      const next = { ...acting.value }
      delete next[card.id]
      acting.value = next
    }
  }

  return { cards, notices, loading, loadedOnce, scanning, scanNote, results, acting, start, refresh, scan, decide }
})

async function runDecide(card: BoardCard, approved: boolean, confirmDestructive: boolean): Promise<string> {
  const decide = card.decide
  if (!decide) return ''
  if (decide.kind === 'resume') {
    const outcome = await resumePlannedRun(decide.url)
    return outcome.ok ? '已继续' : outcome.message
  }
  if (decide.kind === 'run-now') {
    const outcome = await postJson(decide.url, {})
    return outcome.ok ? '已触发' : outcome.message
  }
  if (decide.kind === 'agent') {
    const outcome = await postJson(`/api/ai-agent/approvals/${decide.id}/decide`, { approved })
    if (!outcome.ok) return outcome.message
    return approved ? '已同意，原任务继续' : '已拒绝'
  }
  if (decide.kind === 'db') {
    const outcome = await postJson(`/api/db-admin/approvals/${decide.id}/resolve`, {
      decision: approved ? 'approve' : 'reject',
      confirmDestructive: approved && confirmDestructive,
    })
    if (!outcome.ok) return outcome.message
    return approved ? '已同意，原任务继续' : '已拒绝'
  }
  if (decide.kind === 'terminal') {
    const outcome = await postJson(`/api/smart-terminal/approvals/${decide.id}/resolve`, {
      decision: approved ? 'approve' : 'reject',
    })
    if (!outcome.ok) return outcome.message
    return approved ? '已同意，原任务继续' : '已拒绝'
  }
  const outcome = await postJson(
    `/api/agent-hub/tasks/${decide.taskId}/permissions/${encodeURIComponent(decide.requestId)}/resolve`,
    { allowed: approved, note: '' },
  )
  if (!outcome.ok) return outcome.message
  return approved ? '已同意，只放行这一次' : '已拒绝'
}
