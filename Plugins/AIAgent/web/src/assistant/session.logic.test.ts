import { describe, expect, it } from 'vitest'
import { applyEvent, emptyThreadState, extractCodeFences, primaryKind, splitProse, statusLabel, toolApprovalCards, toolExpandText } from './session.logic'

describe('primaryKind', () => {
  it('yields the header button when a plan awaits confirm', () => {
    expect(primaryKind({ status: 'awaiting_plan', hasPlan: true, approvalPending: false })).toBe('confirm-plan')
  })
  it('lets approval take the gold button', () => {
    expect(primaryKind({ status: 'awaiting_approval', hasPlan: true, approvalPending: true })).toBe('approve')
  })
  it('yields the header button when a run failed and can retry', () => {
    expect(primaryKind({ status: 'failed', hasPlan: false, approvalPending: false })).toBe('retry')
  })
  it('defaults to new session', () => {
    expect(primaryKind({ status: 'idle', hasPlan: false, approvalPending: false })).toBe('new')
  })
})

describe('toolApprovalCards', () => {
  it('只留下这条会话里还没决定的工具', () => {
    const cards = toolApprovalCards([
      { id: 3, sessionId: 'evidence-session', toolName: 'delete_file', description: '删除 evidence-delete.txt', riskLevel: 'high', status: 'Pending' },
      { id: 4, sessionId: 'other', toolName: 'write_file', description: '别的会话', status: 'pending' },
      { id: 5, sessionId: 'evidence-session', toolName: 'write_file', description: '已经决定', status: 'approved' },
    ], 'evidence-session')
    expect(cards).toEqual([
      { id: '3', summary: 'delete_file · 删除 evidence-delete.txt', risk: 'high', action: 'delete_file' },
    ])
  })
})

describe('statusLabel', () => {
  it('uses product wording', () => {
    expect(statusLabel('awaiting_plan')).toBe('计划待确认')
    expect(statusLabel('stuck')).toBe('已卡住')
  })
})

describe('applyEvent', () => {
  it('appends streaming text and completes it', () => {
    let s = emptyThreadState()
    s = applyEvent(s, {
      type: 'message.delta',
      sessionId: 's1',
      seq: 1,
      ts: '',
      payload: { messageId: 'm1', text: '你' },
    })
    s = applyEvent(s, {
      type: 'message.delta',
      sessionId: 's1',
      seq: 2,
      ts: '',
      payload: { messageId: 'm1', text: '好' },
    })
    s = applyEvent(s, {
      type: 'message.completed',
      sessionId: 's1',
      seq: 3,
      ts: '',
      payload: { messageId: 'm1', text: '你好' },
    })
    expect(s.items).toEqual([{ kind: 'assistant', id: 'm1', text: '你好', streaming: false }])
  })

  it('opens the plan panel when a plan is proposed', () => {
    let s = emptyThreadState('running')
    s = applyEvent(s, {
      type: 'plan.proposed',
      sessionId: 's1',
      seq: 1,
      ts: '',
      payload: { goal: '部署', steps: [{ title: '检查' }] },
    })
    expect(s.status).toBe('awaiting_plan')
    expect(s.autoOpenPanel).toBe('plan')
    expect(s.plan?.goal).toBe('部署')
  })

  it('passes through plan note (planning-fallback marker)', () => {
    let s = emptyThreadState('running')
    s = applyEvent(s, {
      type: 'plan.proposed',
      sessionId: 's1',
      seq: 1,
      ts: '',
      payload: { goal: '部署', note: '规划失败，已用回退计划', steps: [{ title: '检查' }] },
    })
    expect(s.plan?.note).toBe('规划失败，已用回退计划')
  })
})

describe('splitProse', () => {
  it('keeps plain text', () => {
    expect(splitProse('你好')).toEqual([{ type: 'text', text: '你好' }])
  })
  it('splits a fenced block', () => {
    const parts = splitProse('见下面：\n```bash\nls -la\n```\n完成')
    expect(extractCodeFences('见下面：\n```bash\nls -la\n```\n完成')).toEqual([{ language: 'bash', content: 'ls -la' }])
    expect(parts).toEqual([
      { type: 'text', text: '见下面：\n' },
      { type: 'code', language: 'bash', content: 'ls -la' },
      { type: 'text', text: '\n完成' },
    ])
  })
})

describe('applyEvent cancel', () => {
  it('marks stuck cards cancelled and clears stuck state', () => {
    let s = emptyThreadState('stuck')
    s = applyEvent(s, {
      type: 'run.stuck',
      sessionId: 's1',
      runId: 'r1',
      seq: 1,
      ts: '',
      payload: { reason: '步骤回合未调用 complete_step', allowedActions: ['skip', 'cancel'] },
    })
    expect(s.status).toBe('stuck')
    s = applyEvent(s, {
      type: 'run.cancelled',
      sessionId: 's1',
      runId: 'r1',
      seq: 2,
      ts: '',
      payload: {},
    })
    expect(s.status).toBe('cancelled')
    expect(s.stuck).toBeNull()
    const card = s.items.find((it) => it.kind === 'stuck')
    expect(card?.kind === 'stuck' && card.stuck.outcome).toBe('cancelled')
    expect(card?.kind === 'stuck' && card.stuck.resolved).toBe(true)
    const stuckCount = s.items.filter((it) => it.kind === 'stuck').length
    s = applyEvent(s, {
      type: 'run.stuck',
      sessionId: 's1',
      runId: 'r1',
      seq: 3,
      ts: '',
      payload: { reason: '迟到的卡住' },
    })
    expect(s.status).toBe('cancelled')
    expect(s.items.filter((it) => it.kind === 'stuck').length).toBe(stuckCount)
  })
})

describe('toolExpandText', () => {
  it('falls back to summary when detail is missing', () => {
    expect(toolExpandText({ summary: 'list_files · 已完成' })).toBe('list_files · 已完成')
  })
  it('prefers detail', () => {
    expect(toolExpandText({ detail: '{ "ok": true }', summary: '已完成' })).toBe('{ "ok": true }')
  })
})

describe('applyEvent tool.completed', () => {
  it('stringifies object result into detail so the row can expand', () => {
    let s = emptyThreadState()
    s = applyEvent(s, {
      type: 'tool.completed',
      sessionId: 's1',
      runId: 'r1',
      seq: 1,
      ts: '',
      payload: { toolCallId: 't1', name: 'list_files', summary: '已完成', result: { success: true, root: '/tmp' } },
    })
    const item = s.items.find((it) => it.kind === 'tool')
    expect(item?.kind === 'tool' && item.tool.detail?.includes('/tmp')).toBe(true)
    expect(item?.kind === 'tool' && toolExpandText(item.tool).includes('/tmp')).toBe(true)
  })
  it('can still expand from summary alone', () => {
    let s = emptyThreadState()
    s = applyEvent(s, {
      type: 'tool.completed',
      sessionId: 's1',
      runId: 'r1',
      seq: 1,
      ts: '',
      payload: { toolCallId: 't2', name: 'list_files', summary: '列出 /tmp' },
    })
    const item = s.items.find((it) => it.kind === 'tool')
    expect(item?.kind === 'tool' && !item.tool.detail).toBe(true)
    expect(item?.kind === 'tool' && toolExpandText(item.tool)).toBe('列出 /tmp')
  })
})
