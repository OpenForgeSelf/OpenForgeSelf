import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

/**
 * PILOT-087（输入1 2026-11-23）停止反馈缺陷：点击停止必须「无论什么结果都有反馈」。
 * 构造输入说明：这里的 404 / already-stopped / failed 响应与命令均为「模拟异常态 / 纯机制边界」
 * 样本，不与真实档案重名（harness 模式同 regression-fixes.spec.ts）。
 * 对应验收标准：02-spec AC-F09-1..7 / AC-T3。
 */

const mem = new Map<string, string>()
vi.stubGlobal('localStorage', {
  getItem: (k: string) => (mem.has(k) ? mem.get(k)! : null),
  setItem: (k: string, v: string) => void mem.set(k, v),
  removeItem: (k: string) => void mem.delete(k),
  clear: () => mem.clear(),
})

const http = vi.hoisted(() => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiDelete: vi.fn(),
  browseDirectories: vi.fn(),
  fetchPluginVersion: vi.fn(),
}))
vi.mock('../http', () => http)

type Store = typeof import('./workbenchStore')
let wb: Store

/** 隔离的单个项目 + 单条命令，用于停止反馈与状态收敛断言。 */
const project = {
  id: 1, name: '停止反馈验收', path: '/verify/stop-feedback', type: '', description: '', tags: '',
  source: 'local', isGitRepo: false, pathExists: true, openDate: '',
  favorite: 0,
  commands: [{ id: 11, name: 'dev', script: 'echo OK', url: null, createdAt: '', updatedAt: '' }],
} as never

beforeEach(async () => {
  mem.clear()
  vi.clearAllMocks()
  vi.resetModules()
  vi.useFakeTimers()
  wb = await import('./workbenchStore')
  wb.store.projects = [project]
  wb.store.currentProjectId = 1
  wb.store.mode = 'live'
})

afterEach(() => {
  wb.disposeWorkbench()
  vi.useRealTimers()
})

const notFound = () => new Error('请求失败(404): 未找到存活的会话')

describe('PILOT-087 运行中日志 404 必须核对会话并收敛（AC-F09-7 / PC-024 lens）', () => {
  it('日志 404 且 /api/runs 无该会话：状态收敛为已结束、留痕一次、登记失效终结轮询', async () => {
    wb.store.taskStatus[11] = 'running'
    http.apiGet.mockImplementation((url: string) => {
      if (String(url).includes('/logs?')) return Promise.reject(notFound())
      return Promise.resolve({ total: 0, runs: [] })
    })

    await wb.pullTaskLogs(11)

    expect(wb.taskStatusOf(11)).toBe('done')
    expect(wb.taskLogsOf(11).some((l) => l.includes('已无该会话'))).toBe(true)
    // 终态必须终结轮询面（PC-024 不变量）：登记失效后，选中不再打网络请求
    expect(wb.isLogUnavailable(11)).toBe(true)
    const calls = http.apiGet.mock.calls.length
    await wb.selectTask(11)
    expect(http.apiGet.mock.calls.length).toBe(calls)
  })

  it('日志 404 但 /api/runs 仍有本面板会话（启动瞬态）：维持继续轮询语义，不误收敛', async () => {
    wb.store.taskStatus[11] = 'running'
    http.apiGet.mockImplementation((url: string) => {
      if (String(url).includes('/logs?')) return Promise.reject(notFound())
      return Promise.resolve({
        total: 1,
        runs: [{
          commandId: 11, projectId: 1, projectName: '停止反馈验收', commandName: 'dev',
          pid: 100, startedAt: '2026-11-23T10:00:00', origin: 'Launched',
        }],
      })
    })

    await wb.pullTaskLogs(11)

    expect(wb.taskStatusOf(11)).toBe('running')
    expect(wb.isLogUnavailable(11)).toBe(false)
  })
})

describe('PILOT-087 stopTask 三种结果都有反馈（AC-F09-1..6）', () => {
  it('outcome=stopped：状态收敛 + 「已停止」反馈 + 日志留痕', async () => {
    wb.store.taskStatus[11] = 'running'
    http.apiPost.mockResolvedValue({ success: true, outcome: 'stopped', message: '已停止进程树（PID 100）' })
    http.apiGet.mockResolvedValue({ total: 0, runs: [] })

    const r = await wb.stopTask(11, 'dev')

    expect(r.outcome).toBe('stopped')
    expect(wb.taskStatusOf(11)).toBe('done')
    expect(wb.store.toasts.some((t) => t.text.includes('已停止'))).toBe(true)
    expect(wb.taskLogsOf(11).some((l) => l.startsWith('■ 已停止'))).toBe(true)
  })

  it('outcome=already-stopped：不算失败，明确提示「已不在运行」并收敛状态', async () => {
    wb.store.taskStatus[11] = 'running'
    http.apiPost.mockResolvedValue({ success: true, outcome: 'already-stopped', message: '该命令没有正在运行的进程' })
    http.apiGet.mockResolvedValue({ total: 0, runs: [] })

    const r = await wb.stopTask(11, 'dev')

    expect(r.outcome).toBe('already-stopped')
    expect(wb.taskStatusOf(11)).toBe('done')
    expect(wb.store.toasts.some((t) => t.text.includes('已不在运行'))).toBe(true)
    expect(wb.store.toasts.some((t) => t.text.startsWith('停止失败'))).toBe(false)
    expect(wb.taskLogsOf(11).some((l) => l.includes('已不在运行'))).toBe(true)
  })

  it('outcome=failed：报「停止失败」含原因，状态保持运行中可重试，日志留痕', async () => {
    wb.store.taskStatus[11] = 'running'
    http.apiPost.mockRejectedValue(new Error('请求失败(409): 进程仍在运行或无权限'))

    const r = await wb.stopTask(11, 'dev')

    expect(r.outcome).toBe('failed')
    expect(wb.taskStatusOf(11)).toBe('running')
    expect(wb.store.toasts.some((t) => t.text.includes('停止失败'))).toBe(true)
    expect(wb.taskLogsOf(11).some((l) => l.includes('停止失败'))).toBe(true)
  })

  it('未运行时点击停止：不发请求，给出「没有运行中的进程」反馈（修无声禁用）', async () => {
    http.apiGet.mockResolvedValue({ total: 0, runs: [] })

    const r = await wb.stopTask(11, 'dev')

    expect(r.outcome).toBe('not-running')
    expect(http.apiPost).not.toHaveBeenCalled()
    expect(wb.store.toasts.some((t) => t.text.includes('没有运行中的进程'))).toBe(true)
    expect(wb.store.toasts.some((t) => t.text.includes('已停止'))).toBe(false)
  })
})

describe('输入4 终态任务必须退出常驻轮询面（PC-024 第4发 · 选中态逃逸面 / AC-F09-8）', () => {
  const logsCalls = () => http.apiGet.mock.calls.filter((c) => String(c[0]).includes('/logs?')).length

  it('任务经日志收敛为已结束后：恰补拉一针尾部日志，之后轮询循环清空不再为它发请求', async () => {
    http.apiGet.mockImplementation((url: string) => {
      if (String(url).includes('/logs?')) return Promise.resolve({ lines: [], nextIndex: 5, running: false })
      return Promise.resolve({ total: 0, runs: [] })
    })
    wb.store.taskStatus[11] = 'running'

    await wb.selectTask(11) // 选中即拉一针 → running:false → 收敛为 done

    expect(wb.taskStatusOf(11)).toBe('done')
    const afterConverge = logsCalls()
    await vi.advanceTimersByTimeAsync(1200) // 1 tick：允许恰一针补拉（抓后端「进程已退出」尾部行）
    const afterGrace = logsCalls()
    expect(afterGrace - afterConverge).toBeLessThanOrEqual(1)
    await vi.advanceTimersByTimeAsync(4800) // 4 ticks：终态后必须归零增长
    expect(logsCalls()).toBe(afterGrace)
  })

  it('选中已完成的历史任务：只拉选中那一针，不进常驻轮询', async () => {
    wb.store.taskStatus[11] = 'done'
    http.apiGet.mockResolvedValue({ lines: ['[10:00:00] 旧输出'], nextIndex: 9, running: false })

    await wb.selectTask(11)

    expect(logsCalls()).toBe(1)
    await vi.advanceTimersByTimeAsync(6000)
    expect(logsCalls()).toBe(1)
  })
})
