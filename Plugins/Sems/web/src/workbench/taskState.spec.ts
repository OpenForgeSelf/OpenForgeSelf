import { beforeEach, describe, expect, it, vi } from 'vitest'

/**
 * v0.2 任务状态机与日志分桶的回归测试。
 * 覆盖用户点名的三个缺陷：
 *  - D1 日志不实时 / 拉不到
 *  - D2 运行中命令运行记录突然消失
 *  - D3 缺少任务状态机
 */

const store = new Map<string, string>()

vi.stubGlobal('localStorage', {
  getItem: (k: string) => (store.has(k) ? store.get(k)! : null),
  setItem: (k: string, v: string) => void store.set(k, v),
  removeItem: (k: string) => void store.delete(k),
  clear: () => store.clear(),
})

type Store = typeof import('./workbenchStore')
let wb: Store

beforeEach(async () => {
  store.clear()
  vi.resetModules()
  wb = await import('./workbenchStore')
})

describe('任务状态机', () => {
  it('空闲 → 运行中 → 已完成，结束后状态不被清掉', () => {
    expect(wb.taskStatusOf(7)).toBe('idle')
    wb.store.taskStatus[7] = 'running'
    expect(wb.taskStatusOf(7)).toBe('running')
    wb.store.taskStatus[7] = 'done'
    expect(wb.taskStatusOf(7)).toBe('done')
  })

  it('不同命令各自独立分桶，切换任务日志不丢', () => {
    wb.store.logsByCommand[1] = ['a1']
    wb.store.logsByCommand[2] = ['b1']
    expect(wb.taskLogsOf(1)).toEqual(['a1'])
    expect(wb.taskLogsOf(2)).toEqual(['b1'])
    expect(wb.taskLogsOf(1)).toEqual(['a1'])
    expect(wb.taskLogsOf(null)).toEqual([])
  })

  it('运行历史持久化：写入 localStorage 后可读回', async () => {
    wb.store.runHistory[3] = {
      name: 'build',
      script: 'pnpm build',
      status: 'done',
      startedAt: '2026-01-01T00:00:00.000Z',
      endedAt: '2026-01-01T00:00:05.000Z',
    }
    // 触发持久化
    wb.saveRunHistory(wb.store.runHistory)
    const raw = store.get('sems-run-history-v1')
    expect(raw).toBeTruthy()
    expect(JSON.parse(raw!)).toHaveProperty('3')
  })
})

describe('任务派生', () => {
  it('projectTasks 按命令生成任务，状态取自状态机', () => {
    wb.store.projects = [
      {
        id: 1, name: 'demo', path: '/tmp/demo', type: 'web', description: '', tags: '',
        source: 'local', isGitRepo: false, pathExists: true, openDate: '2026-01-01',
        favorite: 0,
        commands: [
          { id: 11, name: 'serve', script: 'pnpm serve', url: null, createdAt: '', updatedAt: '' },
          { id: 12, name: 'build', script: 'pnpm build', url: null, createdAt: '', updatedAt: '' },
        ],
      } as never,
    ]
    wb.store.currentProjectId = 1
    wb.store.taskStatus[11] = 'running'
    wb.store.taskFilter = ''
    const tasks = wb.projectTasks(wb.currentProject())
    expect(tasks.map((t) => t.name)).toEqual(['serve', 'build'])
    expect(tasks[0].status).toBe('running')
    expect(tasks[1].status).toBe('idle')
  })
})

describe('项目列表筛选与排序', () => {
  const projects = [
    { id: 1, name: 'alpha', path: '/a', type: 'web', tags: '', source: 'local', isGitRepo: false,
      pathExists: true, openDate: '2026-01-01', favorite: 0, commands: [] },
    { id: 2, name: 'beta', path: '/b', type: 'tool', tags: '', source: 'local', isGitRepo: false,
      pathExists: true, openDate: '2026-03-01', favorite: 1, commands: [] },
    { id: 3, name: 'gamma', path: '/c', type: 'web', tags: '', source: 'local', isGitRepo: false,
      pathExists: true, openDate: '2026-02-01', favorite: 0, commands: [] },
  ] as never[]

  beforeEach(() => {
    wb.store.projects = projects as never
    wb.store.filter = ''
    wb.store.typeFilter = ''
  })

  it('收藏永远置顶，其余按最近活跃排序', () => {
    wb.store.projectSort = 'recent'
    expect(wb.listProjects().map((p) => p.id)).toEqual([2, 3, 1])
  })

  it('类型筛选只保留匹配项，清空后恢复全部', () => {
    wb.store.typeFilter = 'web'
    expect(wb.listProjects().map((p) => p.id).sort()).toEqual([1, 3])
    wb.store.typeFilter = ''
    expect(wb.listProjects().length).toBe(3)
  })

  it('按名称排序', () => {
    wb.store.projectSort = 'name'
    expect(wb.listProjects().map((p) => p.id)).toEqual([2, 1, 3])
  })

  it('projectTypes 汇总全部类型', () => {
    expect(wb.projectTypes()).toEqual(['tool', 'web'])
  })
})

describe('端口派生', () => {
  it('从命令 url 解析端口并标记运行状态', () => {
    const p = {
      id: 1, name: 'x', path: '/x', type: 'web', tags: '', source: 'local', isGitRepo: false,
      pathExists: true, openDate: '', favorite: 0,
      commands: [
        { id: 21, name: 'serve', script: 'pnpm serve', url: 'http://localhost:8080', createdAt: '', updatedAt: '' },
        { id: 22, name: 'api', script: 'pnpm api', url: 'http://127.0.0.1:3000/x', createdAt: '', updatedAt: '' },
        { id: 23, name: 'none', script: 'pnpm none', url: null, createdAt: '', updatedAt: '' },
        { id: 24, name: 'nourl', script: 'pnpm n', url: 'not-a-url', createdAt: '', updatedAt: '' },
      ],
    } as never
    wb.store.taskStatus[22] = 'running'
    const ports = wb.projectPorts(p)
    expect(ports.map((x) => x.port)).toEqual([3000, 8080])
    expect(ports[0].running).toBe(true)
    expect(ports[1].running).toBe(false)
  })
})
