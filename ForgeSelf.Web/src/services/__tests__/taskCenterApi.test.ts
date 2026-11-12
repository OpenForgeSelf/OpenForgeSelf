import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { setStoredToken } from '@/services/request'
import {
  fetchTaskCenterItems,
  mapSchedulerTask,
  resumePlannedRun,
  runSchedulerNow,
  schedulerFailureFromLog,
} from '@/services/taskCenterApi'
import type { SchedulerTaskDto } from '@/services/taskCenterApi'

/**
 * 任务中心 API 层单测（战役 B3）：
 * 纯映射/归一断言（mapSchedulerTask 状态字典）+ 三源聚合容错（fetchTaskCenterItems 桶级降级）
 * + 续跑 SSE 流消费与立即运行封套判定。全部经 vi.stubGlobal('fetch') 零真实网络。
 */

function schedTask(overrides: Partial<SchedulerTaskDto> = {}): SchedulerTaskDto {
  return {
    id: 5,
    name: '夜间巡检',
    taskType: 3,
    status: 0,
    lastRunTime: '2026-11-02T08:00:00',
    nextRunTime: '2026-11-03T08:00:00',
    runCount: 3,
    failureCount: 0,
    ...overrides,
  }
}

describe('mapSchedulerTask · Scheduler 行 → 统一形状', () => {
  it('非 AgentRun 类型（taskType≠3）→ null（本批只收 MAF Run 触发器）', () => {
    expect(mapSchedulerTask(schedTask({ taskType: 1 }))).toBeNull()
  })

  it('Enabled(0) → scheduled，可立即运行，不可重试', () => {
    const item = mapSchedulerTask(schedTask({ status: 0 }))!
    expect(item.status).toBe('scheduled')
    expect(item.id).toBe('sched:5')
    expect(item.source).toBe('scheduler')
    expect(item.retryable).toBe(false)
    expect(item.runNowUrl).toBe('/api/scheduler/tasks/5/run-now')
    expect(item.nextRunAt).toBe('2026-11-03T08:00:00')
  })

  it('Paused(2) → pending；Disabled(1) → cancelled', () => {
    expect(mapSchedulerTask(schedTask({ status: 2 }))!.status).toBe('pending')
    expect(mapSchedulerTask(schedTask({ status: 1 }))!.status).toBe('cancelled')
  })

  it('Error(3) → failed，failureReason 带失败计数，retryable', () => {
    const item = mapSchedulerTask(schedTask({ status: 3, failureCount: 4 }))!
    expect(item.status).toBe('failed')
    expect(item.retryable).toBe(true)
    expect(item.failureReason).toContain('4')
  })

  it('名称为空 → 回退「定时任务 #id」', () => {
    const item = mapSchedulerTask(schedTask({ name: '' }))!
    expect(item.title).toBe('定时任务 #5')
  })
})

describe('schedulerFailureFromLog · 日志原文优选', () => {
  it('优先 errorMessage，其次 resultMessage，全空 → null', () => {
    expect(schedulerFailureFromLog('boom', 'ok')).toBe('boom')
    expect(schedulerFailureFromLog('', 'ok')).toBe('ok')
    expect(schedulerFailureFromLog(null, null)).toBeNull()
  })
})

describe('fetchTaskCenterItems · 三源聚合容错', () => {
  beforeEach(() => setStoredToken('sk-test'))
  afterEach(() => vi.unstubAllGlobals())

  function stubFetchOnce(responses: Array<Promise<Response>>): void {
    let call = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(() => responses[call++]),
    )
  }

  function jsonResponse(body: unknown): Promise<Response> {
    return Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve(body),
    } as Response)
  }

  function errorResponse(status: number): Promise<Response> {
    return Promise.resolve({ ok: false, status, json: () => Promise.resolve({}) } as Response)
  }

  it('两源成功 → 合并 open/planned/scheduler 条目', async () => {
    stubFetchOnce([
      jsonResponse({
        success: true,
        generatedAt: '2026-11-02T21:00:00+08:00',
        sources: [
          { source: 'open', count: 1, ok: true },
          { source: 'planned', count: 1, ok: true },
        ],
        items: [
          { id: 'open:s1', source: 'open', title: '开放任务A', status: 'running', retryable: false,
            startedAt: '2026-10-30T09:00:00+08:00', sourceRef: 's1' },
          {
            id: 'plan:7',
            source: 'planned',
            title: '计划任务B',
            status: 'failed',
            failureReason: 'boom',
            retryable: true,
            retryUrl: '/api/ai-agent/runs/7/resume',
            startedAt: '2026-11-01T10:00:00+08:00',
            sourceRef: '7',
          },
        ],
      }),
      jsonResponse({
        success: true,
        data: { items: [schedTask()], total: 1, page: 1, pageSize: 100 },
      }),
    ])

    const { items, errors } = await fetchTaskCenterItems()
    // T8 起跨桶统一倒序：open(10-30) < plan(11-01) < sched(11-02) → sched 最前
    expect(items.map((x) => x.id)).toEqual(['sched:5', 'plan:7', 'open:s1'])
    expect(errors).toEqual({})
  })

  it('T8：跨桶统一按 startedAt 倒序（最新在上）', async () => {
    stubFetchOnce([
      jsonResponse({
        success: true,
        sources: [
          { source: 'open', count: 1, ok: true },
          { source: 'planned', count: 1, ok: true },
        ],
        items: [
          { id: 'open:s1', source: 'open', title: '旧会话任务', status: 'running', retryable: false,
            startedAt: '2026-11-01T09:00:00+08:00', sourceRef: 's1' },
          { id: 'plan:7', source: 'planned', title: '新计划任务', status: 'running', retryable: false,
            startedAt: '2026-11-03T10:00:00+08:00', sourceRef: '7' },
        ],
      }),
      jsonResponse({
        success: true,
        data: { items: [schedTask()], total: 1, page: 1, pageSize: 100 },
      }),
    ])

    const { items } = await fetchTaskCenterItems()
    // 输入序 open(11-01)→planned(11-03)→sched(11-02)；跨桶倒序后应 plan:7 → sched:5 → open:s1
    expect(items.map((i) => i.id)).toEqual(['plan:7', 'sched:5', 'open:s1'])
  })

  it('Scheduler 失败 → open/planned 条目仍在，errors.scheduler 带原文', async () => {
    stubFetchOnce([
      jsonResponse({ success: true, sources: [{ source: 'open', count: 1, ok: true }], items: [
        { id: 'open:s1', source: 'open', title: 'A', status: 'running', retryable: false, sourceRef: 's1' },
      ] }),
      errorResponse(500),
    ])

    const { items, errors } = await fetchTaskCenterItems()
    expect(items).toHaveLength(1)
    expect(errors.scheduler).toContain('500')
  })

  it('总览整体失败 → 两桶同报错，Scheduler 条目仍在', async () => {
    stubFetchOnce([
      errorResponse(401),
      jsonResponse({ success: true, data: { items: [schedTask()], total: 1, page: 1, pageSize: 100 } }),
    ])

    const { items, errors } = await fetchTaskCenterItems()
    expect(items.map((i) => i.source)).toEqual(['scheduler'])
    expect(errors.open).toBeTruthy()
    expect(errors.planned).toBeTruthy()
  })
})

/** 构造 SSE 流的 Response（resumePlannedRun 走 body.getReader()）。 */
function sseResponse(lines: string[], ok = true): Promise<Response> {
  const encoder = new TextEncoder()
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const line of lines) controller.enqueue(encoder.encode(`${line}\n`))
      controller.close()
    },
  })
  return Promise.resolve({ ok, status: 200, body: stream } as unknown as Response)
}

describe('resumePlannedRun · SSE 流消费', () => {
  beforeEach(() => setStoredToken('sk-test'))
  afterEach(() => vi.unstubAllGlobals())

  it('done 事件 → ok，message 取事件文本', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => sseResponse(['data: {"type":"message","content":"恢复执行"}', 'data: {"type":"done","content":"完成"}'])),
    )
    const outcome = await resumePlannedRun('/api/ai-agent/runs/7/resume')
    expect(outcome.ok).toBe(true)
    expect(outcome.message).toBe('完成')
  })

  it('error 事件 → 失败并透出原文', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => sseResponse(['data: {"type":"error","content":"模型超时"}'])),
    )
    const outcome = await resumePlannedRun('/api/ai-agent/runs/7/resume')
    expect(outcome.ok).toBe(false)
    expect(outcome.message).toBe('模型超时')
  })

  it('流结束无终局事件 → 视为已发起成功', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => sseResponse(['data: {"type":"message","content":"进行中"}'])),
    )
    const outcome = await resumePlannedRun('/api/ai-agent/runs/7/resume')
    expect(outcome.ok).toBe(true)
  })

  it('非 2xx → 失败带状态码', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve({ ok: false, status: 404, json: () => Promise.resolve({}) } as Response)),
    )
    const outcome = await resumePlannedRun('/api/ai-agent/runs/404/resume')
    expect(outcome.ok).toBe(false)
    expect(outcome.message).toContain('404')
  })

  it('Authorization 头携带本地 token', async () => {
    const fetchMock = vi.fn((_url: unknown, _init?: RequestInit) => sseResponse(['data: {"type":"done"}']))
    vi.stubGlobal('fetch', fetchMock)
    await resumePlannedRun('/api/ai-agent/runs/7/resume')
    const init = fetchMock.mock.calls[0]?.[1] as RequestInit | undefined
    expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer sk-test')
  })
})

describe('runSchedulerNow · 立即运行封套', () => {
  beforeEach(() => setStoredToken('sk-test'))
  afterEach(() => vi.unstubAllGlobals())

  it('success=true → ok', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ success: true, message: '已触发' }) } as Response)),
    )
    const outcome = await runSchedulerNow('/api/scheduler/tasks/5/run-now')
    expect(outcome.ok).toBe(true)
    expect(outcome.message).toBe('已触发')
  })

  it('封套 success=false → 失败透出 message', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ success: false, message: '任务已禁用' }) } as Response)),
    )
    const outcome = await runSchedulerNow('/api/scheduler/tasks/5/run-now')
    expect(outcome.ok).toBe(false)
    expect(outcome.message).toBe('任务已禁用')
  })

  it('网络异常 → 失败', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new Error('ECONNREFUSED'))))
    const outcome = await runSchedulerNow('/api/scheduler/tasks/5/run-now')
    expect(outcome.ok).toBe(false)
    expect(outcome.message).toBe('ECONNREFUSED')
  })
})