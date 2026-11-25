import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

/**
 * 2026-11-15 全量复验 5 项整改的机制级回归测试。
 * 构造输入说明：这里的 404 错误、隔离项目与命令都是「模拟异常态 / 纯机制边界」样本，
 * 不是与真实档案重名的硬编码数据；持久化断言读 localStorage 的真实写入值。
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

/** 隔离的单个项目 + 单条命令，用于状态机与日志分桶断言。 */
const project = {
  id: 1, name: '验收项目', path: 'D:/verify', type: '', description: '', tags: '',
  source: 'local', isGitRepo: false, pathExists: true, openDate: '',
  favorite: 0,
  commands: [{ id: 11, name: 'verify', script: 'echo OK', url: null, createdAt: '', updatedAt: '' }],
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

describe('B-2 冷态 404 不再反复轮询', () => {
  it('冷态 404：只提示一次、标记不可用，再选中该命令不再打网络请求', async () => {
    http.apiGet.mockRejectedValue(new Error('请求失败(404): 须由本页启动且宿主未重启'))
    await wb.pullTaskLogs(11)
    expect(wb.isLogUnavailable(11)).toBe(true)
    expect(wb.taskLogsOf(11).filter((l) => l.startsWith('! 读取日志失败'))).toHaveLength(1)

    const calls = http.apiGet.mock.calls.length
    await wb.selectTask(11)
    expect(http.apiGet.mock.calls.length).toBe(calls)
  })

  it('运行中命令的 404 属启动瞬态：不停轮询、不往日志写失败提示', async () => {
    wb.store.taskStatus[11] = 'running'
    http.apiGet.mockRejectedValue(new Error('请求失败(404): 日志缓冲尚未建立'))
    await wb.pullTaskLogs(11)
    expect(wb.isLogUnavailable(11)).toBe(false)
    expect(wb.taskLogsOf(11).some((l) => l.startsWith('! 读取日志失败'))).toBe(false)
  })
})

describe('B-1 三条启动路径共用状态入口', () => {
  it('markTaskStarted 同步状态机/历史/日志，并解除冷态停用标记', async () => {
    http.apiGet.mockRejectedValue(new Error('请求失败(404): 冷态'))
    await wb.pullTaskLogs(11)
    expect(wb.isLogUnavailable(11)).toBe(true)

    wb.markTaskStarted(11)
    expect(wb.taskStatusOf(11)).toBe('running')
    expect(wb.taskHistoryOf(11)?.status).toBe('running')
    expect(wb.taskLogsOf(11)[0]).toContain('$ echo OK')
    expect(wb.isLogUnavailable(11)).toBe(false)
  })
})

describe('D-2 显示偏好真实落盘', () => {
  it('开关与布局写入 localStorage，并可由同一 store 读回', () => {
    const p = wb.currentProject()!
    expect(wb.projectConfigs(p).showPathBadge).toBe(true)
    wb.setProjectConfig(p, 'showPathBadge', false)
    expect(wb.projectConfigs(p).showPathBadge).toBe(false)

    wb.setProjectLayout('card')
    const saved = JSON.parse(mem.get('sems-workbench-v1')!)
    expect(saved.configs['1'].showPathBadge).toBe(false)
    expect(saved.projectLayout).toBe('card')
    expect(wb.projectLayout.value).toBe('card')
  })
})

describe('D-1 2 步向导只写回真实档案字段', () => {
  it('POST 登记后 PUT 写回 type/tags/description，不含任何脚手架假字段', async () => {
    http.apiPost.mockResolvedValue({ project: { id: 7, root: 'D:/new-app', name: '新项目' } })
    http.apiPut.mockResolvedValue({})
    http.apiGet.mockResolvedValue({ projects: [], total: 0 })

    await wb.registerProject('D:/new-app', '新项目', {
      type: 'frontend', tags: 'a,b', description: '说明',
    })

    expect(http.apiPut).toHaveBeenCalledTimes(1)
    const [url, body] = http.apiPut.mock.calls[0] as [string, Record<string, unknown>]
    expect(url).toBe('/api/projects/7')
    expect(body).toEqual({ type: 'frontend', tags: 'a,b', description: '说明' })
    expect(JSON.stringify(body)).not.toMatch(/packageManager|preset|git|bare|commitMessage|features/)
  })
})

describe('F03 同一路径再次登记', () => {
  it('已存在的路径不发登记或写回请求，并标 alreadyExisted', async () => {
    const result = await wb.registerProject('D:/verify/', '新名字', {
      type: 'tool', tags: 'x', description: '覆盖',
    })
    expect(result?.alreadyExisted).toBe(true)
    expect(result?.project.id).toBe(1)
    expect(result?.project.name).toBe('验收项目')
    expect(http.apiPost).not.toHaveBeenCalled()
    expect(http.apiPut).not.toHaveBeenCalled()
  })
})

describe('运行页没有命令时不请求日志', () => {
  it('选中不存在的命令时清空选中，且不发日志请求', async () => {
    wb.store.projects = [{ ...project, commands: [] } as never]
    await wb.selectTask(11)
    expect(wb.store.selectedTaskId).toBeNull()
    expect(http.apiGet).not.toHaveBeenCalled()
  })
})

describe('F11 没有命令时启动全部不得空转成功', () => {
  it('命令列表为空时不发启动请求，并说明没有可启动的命令', async () => {
    wb.store.projects = [{ ...project, commands: [] } as never]
    await wb.startAllTasks()
    expect(http.apiPost).not.toHaveBeenCalled()
    expect(wb.store.toasts.some((t) => t.text.includes('没有可启动的命令'))).toBe(true)
    expect(wb.store.toasts.some((t) => t.text.includes('启动完成'))).toBe(false)
  })
})

describe('v1.0.4 运行页一屏：检查到的进程并入命令列表', () => {
  const live = (commandId: number, pid: number, origin = 'Detected') => ({
    commandId, projectId: 1, projectName: '验收项目', commandName: commandId ? 'verify' : 'node', pid,
    startedAt: '', origin,
  })

  it('没有已登记命令时「检查」仍可调用，结果只进运行会话，不写命令档案', async () => {
    wb.store.projects = [{ ...(project as object), commands: [] } as never]
    http.apiPost.mockResolvedValue({ total: 1, runs: [live(0, 4321)] })
    await wb.checkRuns()
    expect(http.apiPost).toHaveBeenCalledWith('/api/runs/check', {})
    expect(http.apiPost).toHaveBeenCalledTimes(1)
    expect(http.apiPut).not.toHaveBeenCalled()
    expect(wb.currentProject()?.commands).toHaveLength(0)
    expect(wb.unmatchedSessionsFor(wb.currentProject()).map((s) => s.pid)).toEqual([4321])
  })

  it('对得上命令的进程只挂在该命令行，不另占一行；对不上的才单独列出', async () => {
    http.apiPost.mockResolvedValue({ total: 2, runs: [live(11, 100), live(0, 200)] })
    await wb.checkRuns()
    expect(wb.liveSessionOfCommand(1, 11)?.pid).toBe(100)
    expect(wb.unmatchedSessionsFor(wb.currentProject()).map((s) => s.pid)).toEqual([200])
  })

  it('按进程标识停止后同步运行会话与计数', async () => {
    wb.store.runningSessions = [live(0, 200)] as never
    http.apiPost.mockResolvedValue({ outcome: 'stopped' })
    http.apiGet.mockResolvedValue({ total: 0, runs: [] })
    await wb.stopSessionByPid(200)
    expect(http.apiPost).toHaveBeenCalledWith('/api/runs/200/stop', {})
    expect(wb.store.runningCount).toBe(0)
    expect(wb.unmatchedSessionsFor(wb.currentProject())).toHaveLength(0)
  })

  it('GET /api/runs 只有本页启动会话时，不整表覆盖检查到的进程', async () => {
    wb.store.runningSessions = [live(11, 100), live(0, 200)] as never
    http.apiGet.mockResolvedValue({ total: 1, runs: [live(11, 100, 'Launched')] })
    await wb.refreshRunningCount()
    expect(wb.liveSessionOfCommand(1, 11)?.origin).toBe('Launched')
    expect(wb.unmatchedSessionsFor(wb.currentProject()).map((s) => s.pid)).toEqual([200])
    expect(wb.store.runningCount).toBe(2)
  })

  it('GET /api/runs 带回检测会话时，以对端列表为准，已退出的检测进程不再保留', async () => {
    wb.store.runningSessions = [live(0, 200), live(0, 300)] as never
    http.apiGet.mockResolvedValue({ total: 1, runs: [live(0, 200)] })
    await wb.refreshRunningCount()
    expect(wb.unmatchedSessionsFor(wb.currentProject()).map((s) => s.pid)).toEqual([200])
  })

  it('选中尚未启动、没有可看会话的命令时不请求日志，也不写 404 失败行', async () => {
    http.apiGet.mockRejectedValue(new Error('请求失败(404): 须由本页启动且宿主未重启'))
    await wb.selectTask(11)
    expect(http.apiGet).not.toHaveBeenCalled()
    expect(wb.taskLogsOf(11).some((l) => l.includes('读取日志失败'))).toBe(false)
    await vi.advanceTimersByTimeAsync(4000)
    expect(http.apiGet).not.toHaveBeenCalled()
  })
})
