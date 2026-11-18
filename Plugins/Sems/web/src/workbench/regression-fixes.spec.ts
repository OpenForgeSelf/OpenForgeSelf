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
    http.apiPost.mockResolvedValue({ project: { id: 7, root: 'D:/verify', name: '验收项目' } })
    http.apiPut.mockResolvedValue({})
    http.apiGet.mockResolvedValue({ projects: [], total: 0 })

    await wb.registerProject('D:/verify', '验收项目', {
      type: 'frontend', tags: 'a,b', description: '说明',
    })

    expect(http.apiPut).toHaveBeenCalledTimes(1)
    const [url, body] = http.apiPut.mock.calls[0] as [string, Record<string, unknown>]
    expect(url).toBe('/api/projects/7')
    expect(body).toEqual({ type: 'frontend', tags: 'a,b', description: '说明' })
    expect(JSON.stringify(body)).not.toMatch(/packageManager|preset|git|bare|commitMessage|features/)
  })
})
