/**
 * 任务中心 API 服务层（战役 2026-11-02-maf-runtime-hardening B3）。
 *
 * 聚合三类任务入口为统一 TaskOverviewItem（契约真源 = 02-b3-taskcenter/02-spec.md §3.2）：
 * ① 开放任务会话 Run + ② 计划任务 AgentRun —— 后端 GET /api/ai-agent/tasks-overview（统一 DTO）；
 * ③ Scheduler AgentRun —— 既有 GET /api/scheduler/tasks（TaskType=3 过滤，前端映射为同一形状）。
 *   后端不聚合 Scheduler 的依据见 02-spec §3.5 决策 D1（插件间禁止程序集引用/HTTP 自调）。
 */
import { getStoredToken } from './request'

/** 统一状态字典（与后端 TaskOverviewStatus 对齐；scheduled 为 Scheduler 桶专属）。 */
export type TaskStatus =
  | 'running'
  | 'planning'
  | 'pending'
  | 'completed'
  | 'failed'
  | 'stuck'
  | 'cancelled'
  | 'scheduled'
  | 'unknown'

/** 任务来源：开放任务会话 / 计划任务 / 定时任务（Scheduler）。 */
export type TaskSource = 'open' | 'planned' | 'scheduler'

/** 跨入口任务统一条目（视图层唯一消费形状）。 */
export interface TaskOverviewItem {
  /** 稳定标识：open:{sessionId} / plan:{runId} / sched:{taskId} */
  id: string
  source: TaskSource
  title: string
  status: TaskStatus
  failureReason?: string | null
  startedAt?: string | null
  endedAt?: string | null
  /** 失败可重试/续跑（failed/stuck） */
  retryable: boolean
  /** planned：POST 续跑端点（SSE 流） */
  retryUrl?: string | null
  /** 终止端点（后端对非终态 Run 下发，POST 后任务进 Cancelled；scheduler 行恒 null；T14⑥）。 */
  cancelUrl?: string | null
  /** scheduler：POST 立即运行端点 */
  runNowUrl?: string | null
  sourceRef: string
  /** scheduler 专属：下次触发时间 / 连续失败次数（其余来源 null/undefined） */
  nextRunAt?: string | null
  failureCount?: number
}

/** 后端总览响应（GET /api/ai-agent/tasks-overview，camelCase）。 */
interface OverviewResponse {
  success: boolean
  generatedAt: string
  sources: Array<{ source: string; count: number; ok: boolean; error?: string | null }>
  items: Array<{
    id: string
    source: 'open' | 'planned'
    title: string
    status: TaskStatus
    failureReason?: string | null
    startedAt?: string | null
    endedAt?: string | null
    retryable: boolean
    retryUrl?: string | null
      cancelUrl?: string | null
    sourceRef: string
  }>
}

/** Scheduler 任务 DTO（GET /api/scheduler/tasks 返回，ScheduledTaskDto 投影）。 */
export interface SchedulerTaskDto {
  id: number
  name: string
  description?: string | null
  taskType: number
  status: number
  lastRunTime?: string | null
  nextRunTime?: string | null
  runCount?: number
  failureCount?: number
}

/** Scheduler 枚举（与 Plugins/Scheduler/Models/SchedulerEnums.cs 对齐）。 */
export const SCHEDULED_TASK_TYPE_AGENT_RUN = 3
const SCHED_STATUS = { Enabled: 0, Disabled: 1, Paused: 2, Error: 3 } as const

/** 单桶失败信息（按来源键控，视图降级展示）。 */
export type BucketErrors = Partial<Record<TaskSource, string>>

export interface TaskCenterSnapshot {
  items: TaskOverviewItem[]
  errors: BucketErrors
}

/**
 * Scheduler 行 → 统一形状；非 AgentRun 类型返回 null（本批只收 MAF Run 触发器）。
 * 状态映射：Enabled→scheduled / Paused→pending / Disabled→cancelled / Error→failed（带失败计数）。
 */
export function mapSchedulerTask(task: SchedulerTaskDto): TaskOverviewItem | null {
  if (task.taskType !== SCHEDULED_TASK_TYPE_AGENT_RUN) return null
  let status: TaskStatus
  switch (task.status) {
    case SCHED_STATUS.Enabled:
      status = 'scheduled'
      break
    case SCHED_STATUS.Paused:
      status = 'pending'
      break
    case SCHED_STATUS.Disabled:
      status = 'cancelled'
      break
    case SCHED_STATUS.Error:
      status = 'failed'
      break
    default:
      status = 'unknown'
  }
  const failed = status === 'failed'
  return {
    id: `sched:${task.id}`,
    source: 'scheduler',
    title: task.name || `定时任务 #${task.id}`,
    status,
    failureReason: failed
      ? `连续失败 ${task.failureCount ?? 0} 次${task.description ? `：${task.description}` : ''}`
      : null,
    startedAt: task.lastRunTime ?? null,
    endedAt: null,
    retryable: failed,
    retryUrl: null,
    cancelUrl: null,
    runNowUrl: `/api/scheduler/tasks/${task.id}/run-now`,
    sourceRef: String(task.id),
    nextRunAt: task.nextRunTime ?? null,
    failureCount: task.failureCount ?? 0,
  }
}

/** Scheduler 行失败详情文案（来自任务日志最近失败记录，视图按需拉取后回填）。 */
/** T8 排序键：startedAt 毫秒时间戳（缺/不可解析=0 排最后）。 */
function startedKey(item: TaskOverviewItem): number {
  const t = Date.parse(item.startedAt ?? '')
  return Number.isNaN(t) ? 0 : t
}

export function schedulerFailureFromLog(errorMessage?: string | null, resultMessage?: string | null): string | null {
  const text = (errorMessage ?? '').trim() || (resultMessage ?? '').trim()
  return text || null
}

/** 聚合三源：后端总览（open+planned）+ Scheduler 任务列表（scheduler）。单源失败不拖垮其余。 */
export async function fetchTaskCenterItems(): Promise<TaskCenterSnapshot> {
  const [overview, scheduler] = await Promise.allSettled([requestOverview(), requestSchedulerTasks()])

  const items: TaskOverviewItem[] = []
  const errors: BucketErrors = {}

  if (overview.status === 'fulfilled') {
    const resp = overview.value
    for (const item of resp.items) {
      items.push({
        id: item.id,
        source: item.source,
        title: item.title,
        status: item.status,
        failureReason: item.failureReason ?? null,
        startedAt: item.startedAt ?? null,
        endedAt: item.endedAt ?? null,
        retryable: item.retryable,
        retryUrl: item.retryUrl ?? null,
        cancelUrl: item.cancelUrl ?? null,
        runNowUrl: null,
        sourceRef: item.sourceRef,
        nextRunAt: null,
        failureCount: undefined,
      })
    }
    for (const bucket of resp.sources) {
      if (!bucket.ok && (bucket.source === 'open' || bucket.source === 'planned')) {
        errors[bucket.source] = bucket.error || '来源数据不可用'
      }
    }
  } else {
    // 总览端点整体失败：两桶同报错（原文透出，不编原因）
    const msg = overview.reason instanceof Error ? overview.reason.message : String(overview.reason)
    errors.open = msg
    errors.planned = msg
  }

  if (scheduler.status === 'fulfilled') {
    for (const task of scheduler.value) {
      const mapped = mapSchedulerTask(task)
      if (mapped) items.push(mapped)
    }
  } else {
    const msg = scheduler.reason instanceof Error ? scheduler.reason.message : String(scheduler.reason)
    errors.scheduler = msg
  }

  // T8（验收#1）：跨桶统一按开始时间倒序（最新在上）——桶内序由后端保证（open=最后活跃 / planned=创建时间 /
  // scheduler=最近运行），拼接序不再决定展示序；无 startedAt 的行排最后，同键保持稳定序（桶内相对序不变）。
  items.sort((a, b) => startedKey(b) - startedKey(a))

  return { items, errors }
}

/** GET /api/ai-agent/tasks-overview（request 约定：带 token，原样返回 JSON）。 */
async function requestOverview(): Promise<OverviewResponse> {
  return fetchJson<OverviewResponse>('/api/ai-agent/tasks-overview?limit=30')
}

/** GET /api/scheduler/tasks（ApiResponse<PagedResult> 封套取 data.items；兼容裸数组）。 */
async function requestSchedulerTasks(): Promise<SchedulerTaskDto[]> {
  const json = await fetchJson<
    { success?: boolean; message?: string; data?: { items?: SchedulerTaskDto[] } } | SchedulerTaskDto[]
  >('/api/scheduler/tasks?page=1&pageSize=100')
  if (Array.isArray(json)) return json
  const items = json?.data?.items
  if (!items) {
    throw new Error(json?.message || '定时任务列表返回结构异常')
  }
  return items
}

/** 带 token 的 fetch（request.ts 同一 token 约定），非 2xx 抛出含状态码/后端原因的错误。 */
async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init?.headers as Record<string, string>),
  }
  const token = getStoredToken()
  if (token) headers.Authorization = `Bearer ${token}`
  const res = await fetch(path, { ...init, headers })
  if (!res.ok) {
    let detail = ''
    try {
      const body = (await res.json()) as { message?: string; title?: string; error?: string }
      detail = body?.message || body?.title || body?.error || ''
    } catch {
      // 非 JSON 响应体忽略
    }
    throw new Error(detail || `请求失败(${res.status})`)
  }
  return (await res.json()) as T
}

export interface ResumeOutcome {
  ok: boolean
  message: string
}

/**
 * 续跑计划任务（POST retryUrl，SSE 流）：读取事件流直到终局事件。
 * done → 成功；error → 失败并透出错误原文；其余事件经 onEvent 透传（行内进度可扩展）。
 */
export async function resumePlannedRun(
  retryUrl: string,
  onEvent?: (type: string, text: string) => void,
): Promise<ResumeOutcome> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  const token = getStoredToken()
  if (token) headers.Authorization = `Bearer ${token}`

  let res: Response
  try {
    res = await fetch(retryUrl, { method: 'POST', headers })
  } catch (e) {
    return { ok: false, message: e instanceof Error ? e.message : '网络错误' }
  }
  if (!res.ok || !res.body) {
    let detail = ''
    try {
      const body = (await res.json()) as { error?: string; message?: string }
      detail = body?.error || body?.message || ''
    } catch {
      // 非 JSON 响应体忽略
    }
    return { ok: false, message: detail || `请求失败(${res.status})` }
  }

  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let lastError = ''
  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n')
    buffer = lines.pop() ?? ''
    for (const line of lines) {
      const trimmed = line.trim()
      if (!trimmed.startsWith('data:')) continue
      const payload = trimmed.slice(5).trim()
      if (!payload) continue
      try {
        const ev = JSON.parse(payload) as { type?: string; content?: string; name?: string }
        const type = ev.type ?? ''
        const text = ev.content ?? ev.name ?? ''
        onEvent?.(type, text)
        if (type === 'error') lastError = text || '执行出错'
        if (type === 'done') return { ok: true, message: text || '续跑完成' }
      } catch {
        // 非 JSON 行（SSE 心跳等）忽略
      }
    }
  }
  if (lastError) return { ok: false, message: lastError }
  return { ok: true, message: '续跑已发起（流已结束）' }
}

/** 立即运行定时任务（POST run-now；封套 success=false 视为失败并透出 message）。 */
export async function runSchedulerNow(runNowUrl: string): Promise<ResumeOutcome> {
  try {
    const json = await fetchJson<{ success?: boolean; message?: string }>(runNowUrl, { method: 'POST' })
    if (json && json.success === false) {
      return { ok: false, message: json.message || '触发失败' }
    }
    return { ok: true, message: json?.message || '已触发立即运行' }
  } catch (e) {
    return { ok: false, message: e instanceof Error ? e.message : '网络错误' }
  }
}

/**
 * 拉取定时任务最近一次失败原因（GET /api/scheduler/tasks/{id}/logs，取最近 Failed 日志）。
 * 拉不到/无失败日志返回 null（视图回退展示「连续失败 N 次」计数文案）。
 */
/** 终止 Run（POST cancelUrl；封套 success=false 视为失败并透出 message）。 */
export async function cancelRun(cancelUrl: string): Promise<ResumeOutcome> {
  try {
    const json = await fetchJson<{ success?: boolean; message?: string }>(cancelUrl, { method: "POST" })
    if (json && json.success === false) {
      return { ok: false, message: json.message || "终止失败" }
    }
    return { ok: true, message: json?.message || "已终止" }
  } catch (e) {
    return { ok: false, message: e instanceof Error ? e.message : "网络错误" }
  }
}


/**
 * 拉取定时任务最近一次失败原因（GET /api/scheduler/tasks/{id}/logs，取最近 Failed 日志）。
 */
export async function fetchSchedulerLastFailure(taskId: number): Promise<string | null> {
  try {
    const json = await fetchJson<{
      data?: { items?: Array<{ status: number; errorMessage?: string | null; resultMessage?: string | null }> }
    }>(`/api/scheduler/tasks/${taskId}/logs?page=1&pageSize=10`)
    const failed = (json?.data?.items ?? []).find((l) => l.status === 1)
    if (!failed) return null
    return schedulerFailureFromLog(failed.errorMessage, failed.resultMessage)
  } catch {
    // 失败原因属增强信息，拉取失败不作为页面级错误
    return null
  }
}