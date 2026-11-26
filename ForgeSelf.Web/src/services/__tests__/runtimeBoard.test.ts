import { describe, expect, it } from 'vitest'
import { attentionCount, blockingCount, classifyRuntime } from '../runtimeBoard'
import type { FetchSlice, RuntimeInput } from '../runtimeBoard'
import { monitorPidFromQuery, queryText, schedulerTaskFromQuery, scriptExecutionFromQuery } from '../runtimeDeepLink'

const now = Date.parse('2026-11-26T02:00:00Z')

function ok<T>(data: T): FetchSlice<T> {
  return { status: 200, data }
}

function base(): RuntimeInput {
  return { now }
}

describe('classifyRuntime', () => {
  it('把破坏性审批放进要你处理，普通审批放进要你看，过期的丢掉', () => {
    const board = classifyRuntime({
      ...base(),
      agentApprovals: ok([
        { id: 1, sessionId: 's1', toolName: 'delete_file', description: '删除缓存', expiresAt: '2026-11-26T02:20:00Z' },
        { id: 2, sessionId: 's1', toolName: 'write_file', description: '写说明', expiresAt: '2026-11-26T02:20:00Z' },
        { id: 3, sessionId: 's2', toolName: 'write_file', description: '已过期', expiresAt: '2026-11-26T01:00:00Z' },
      ]),
    })
    const urgent = board.cards.filter((c) => c.lane === 'urgent')
    const wait = board.cards.filter((c) => c.lane === 'wait')
    expect(urgent.map((c) => c.id)).toEqual(['agent-approval:1'])
    expect(wait.map((c) => c.id)).toEqual(['agent-approval:2'])
    expect(urgent[0]?.sessionGroup).toBe('s1')
    expect(urgent[0]?.link).toEqual({ path: '/ai-agent', query: { session: 's1' } })
    expect(attentionCount(board.cards)).toBe(2)
  })

  it('同一会话已有待批时，不再另开一条在跑；卡住且没有待批才进要续跑', () => {
    const board = classifyRuntime({
      ...base(),
      agentApprovals: ok([
        { id: 9, sessionId: 'live', toolName: 'write_file', expiresAt: '2026-11-26T03:00:00Z' },
      ]),
      overview: ok([
        { id: 'open:live', source: 'open', title: '正在写', status: 'running', sourceRef: 'live' },
        { id: 'open:stuck', source: 'open', title: '停住了', status: 'stuck', sourceRef: 'stuck', retryUrl: '/api/ai-agent/runs/4/retry' },
        { id: 'plan:8', source: 'planned', title: '计划还在跑', status: 'running', sourceRef: '8' },
      ]),
    })
    expect(board.cards.find((c) => c.id === 'run:open:live')).toBeUndefined()
    expect(board.cards.find((c) => c.id === 'resume:open:stuck')?.lane).toBe('resume')
    expect(board.cards.find((c) => c.id === 'resume:open:stuck')?.decide).toEqual({
      kind: 'resume',
      url: '/api/ai-agent/runs/4/retry',
    })
    expect(board.cards.find((c) => c.id === 'run:plan:8')?.link.query).toEqual({ runId: '8' })
  })

  it('待办委派出去的外部任务只留一行，并带来自待办', () => {
    const board = classifyRuntime({
      ...base(),
      todos: [{ id: 12, agentTaskKey: 'task-abc', projectName: '铸己匣', title: '修首页' }],
      hubTasks: ok([{ id: 3, taskKey: 'task-abc', prompt: '修首页', status: 'Running' }]),
    })
    const row = board.cards.find((c) => c.id === 'hub:3')
    expect(row?.lane).toBe('running')
    expect(row?.project).toBe('铸己匣')
    expect(row?.secondaryLink).toEqual({ path: '/todo', query: { todoId: '12' }, label: '来自待办' })
    expect(board.cards.filter((c) => c.lane === 'running')).toHaveLength(1)
  })

  it('外部权限只放行这一次，终端待批写明本次宿主运行期间有效', () => {
    const board = classifyRuntime({
      ...base(),
      hubTasks: ok([{ id: 5, taskKey: 'k', prompt: '改配置', status: 'AwaitingPermission' }]),
      hubPermissions: ok([{ taskId: 5, requestId: 'req-1', toolName: 'shell', reason: '要执行命令' }]),
      terminalApprovals: ok([{ id: 'ap1', sessionId: 'sess', command: 'git status', risk: 'low', status: 'pending' }]),
      terminalSessions: ok([{ id: 'sess', blocks: [{ status: 'running', command: 'git status' }] }]),
    })
    const hub = board.cards.find((c) => c.id === 'hub-permission:5:req-1')
    expect(hub?.once).toBe(true)
    expect(hub?.detail).toContain('只放行这一次')
    expect(hub?.link).toEqual({ path: '/agent-hub', query: { taskId: '5' } })
    const term = board.cards.find((c) => c.id === 'term-approval:ap1')
    expect(term?.note).toBe('本次宿主运行期间有效')
    expect(term?.lane).toBe('wait')
    expect(board.cards.find((c) => c.id === 'term:sess')).toBeUndefined()
  })

  it('终端高危风险值 highRisk 进紧急，不是普通待批', () => {
    const board = classifyRuntime({
      ...base(),
      terminalApprovals: ok([{ id: 'ap-rm', sessionId: 's2', command: 'rm -f out.txt', risk: 'highRisk', status: 'pending' }]),
    })
    expect(board.cards.find((c) => c.id === 'term-approval:ap-rm')?.lane).toBe('urgent')
  })

  it('工程台按 pid 去重；对上项目走工程台，对不上走系统监控', () => {
    const board = classifyRuntime({
      ...base(),
      runs: ok([
        { projectId: 4, projectName: '甲', commandName: 'dev', pid: 100, origin: 'Launched' },
        { projectId: 4, projectName: '甲', commandName: 'dev', pid: 100, origin: 'Detected' },
        { projectId: 0, projectName: '', commandName: 'orphan', pid: 200, origin: 'Detected' },
      ]),
    })
    const rows = board.cards.filter((c) => c.lane === 'running')
    expect(rows.map((c) => c.id)).toEqual(['run-proc:100', 'run-proc:200'])
    expect(rows[0]?.link.path).toBe('/sems')
    expect(rows[0]?.link.query).toEqual({ projectId: '4', runId: '100' })
    expect(rows[1]?.project).toBe('未归属')
    expect(rows[1]?.link).toEqual({ path: '/system-monitor', query: { pid: '200' } })
  })

  it('智能体已经代表的定时触发不再重复；未到点的进待触发', () => {
    const board = classifyRuntime({
      ...base(),
      overview: ok([
        { id: 'open:scheduler:7', source: 'open', title: '定时回合', status: 'running', sourceRef: 'scheduler:7' },
      ]),
      schedulerRunning: ok([{ logId: 1, taskId: 7, name: '日报', startTime: '2026-11-26T01:00:00Z' }]),
      schedulerTasks: ok([
        { id: 7, name: '日报', status: 0, nextRunTime: '2026-11-26T03:00:00Z' },
        { id: 8, name: '周报', status: 0, nextRunTime: '2026-11-27T03:00:00Z' },
      ]),
    })
    expect(board.cards.find((c) => c.id === 'sched-run:1')).toBeUndefined()
    expect(board.cards.find((c) => c.id === 'run:open:scheduler:7')).toBeTruthy()
    expect(board.cards.find((c) => c.id === 'sched:8')?.lane).toBe('scheduled')
    expect(board.cards.find((c) => c.id === 'sched:7')).toBeUndefined()
  })

  it('脚本、工作流、扫描、已打开的浏览器各有一行，并带上对应查询', () => {
    const board = classifyRuntime({
      ...base(),
      scripts: ok([{ id: '11', scriptName: '清理' }]),
      workflows: ok([{ id: '22', workflowName: '发布' }]),
      scans: ok([{ scanId: 'scan-9', rootPath: 'docs' }]),
      browser: ok({ open: true, url: 'https://example.test' }),
    })
    expect(board.cards.find((c) => c.id === 'script:11')?.link).toEqual({
      path: '/script-runner',
      query: { executionId: '11' },
    })
    expect(board.cards.find((c) => c.id === 'wf:22')?.link.query).toEqual({ executionId: '22' })
    expect(board.cards.find((c) => c.id === 'scan:scan-9')?.link.query).toEqual({ scanId: 'scan-9' })
    expect(board.cards.find((c) => c.id === 'browser')?.link.path).toBe('/browser-use')
  })

  it('浏览器没打开时不占一行', () => {
    const board = classifyRuntime({ ...base(), browser: ok({ open: false, url: null }) })
    expect(board.cards.find((c) => c.source === '浏览器')).toBeUndefined()
  })

  it('401 只标记这一路，404 标未启用，其余照常', () => {
    const board = classifyRuntime({
      ...base(),
      dbApprovals: { status: 401, message: 'unauthorized' },
      scans: { status: 404 },
      scripts: ok([{ id: 1, scriptName: '仍在' }]),
    })
    expect(board.notices.map((n) => n.message)).toEqual(['数据库 需要访问密钥', '文件工具 未启用'])
    expect(board.cards.find((c) => c.id === 'script:1')).toBeTruthy()
  })

  it('知道即可只留最近的失败优先，并截断到 20 条', () => {
    const items = Array.from({ length: 25 }, (_, i) => ({
      id: `open:k${i}`,
      source: 'open' as const,
      title: `完成 ${i}`,
      status: i === 0 ? 'failed' : 'completed',
      sourceRef: `k${i}`,
    }))
    const board = classifyRuntime({ ...base(), overview: ok(items) })
    const known = board.cards.filter((c) => c.lane === 'known')
    expect(known).toHaveLength(20)
    expect(known[0]?.id).toBe('known:open:k0')
  })

  it('数据库破坏性待批进紧急，并带 approvalId', () => {
    const board = classifyRuntime({
      ...base(),
      dbApprovals: ok([{ id: 44, sqlRedacted: 'DROP TABLE t', destructive: true, status: 'pending' }]),
    })
    const card = board.cards[0]
    expect(card?.lane).toBe('urgent')
    expect(card?.destructive).toBe(true)
    expect(card?.link).toEqual({ path: '/db-admin', query: { approvalId: '44' } })
  })
})

describe('blockingCount', () => {
  it('已经写下结果的待批不再计入页头数字', () => {
    const cards = [
      { id: 'a', lane: 'urgent' as const, source: '智能体', project: '未归属', title: '删', detail: '', destructive: true, once: false, link: { path: '/ai-agent', query: {} } },
      { id: 'b', lane: 'wait' as const, source: '数据库', project: '未归属', title: '写', detail: '', destructive: false, once: false, link: { path: '/db-admin', query: {} } },
      { id: 'c', lane: 'running' as const, source: '脚本', project: '未归属', title: '跑', detail: '', destructive: false, once: false, link: { path: '/script-runner', query: {} } },
    ]
    expect(attentionCount(cards)).toBe(2)
    expect(blockingCount(cards, { a: '已同意，原任务继续' })).toBe(1)
  })
})

describe('runtimeDeepLink', () => {
  it('带查询参数时取出要打开的详情', () => {
    expect(scriptExecutionFromQuery({ executionId: '11' })).toBe('11')
    expect(scriptExecutionFromQuery({ executionId: ['11', '12'] })).toBe('11')
    expect(schedulerTaskFromQuery({ taskId: '7' })).toBe('7')
    expect(monitorPidFromQuery({ pid: '200' })).toBe(200)
    expect(monitorPidFromQuery({ pid: '0' })).toBe(0)
    expect(queryText({}, 'missing')).toBe('')
  })
})
