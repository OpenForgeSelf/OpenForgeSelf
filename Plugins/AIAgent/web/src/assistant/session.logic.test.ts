import { describe, expect, it } from 'vitest'
import { applyEvent, emptyThreadState, primaryKind, statusLabel } from './session.logic'

describe('primaryKind', () => {
  it('lets confirm-plan take the gold button', () => {
    expect(primaryKind({ status: 'awaiting_plan', hasPlan: true, approvalPending: false })).toBe('confirm-plan')
  })
  it('lets approval take the gold button', () => {
    expect(primaryKind({ status: 'awaiting_approval', hasPlan: true, approvalPending: true })).toBe('approve')
  })
  it('defaults to new session', () => {
    expect(primaryKind({ status: 'idle', hasPlan: false, approvalPending: false })).toBe('new')
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
})
