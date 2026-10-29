import { describe, expect, it } from 'vitest'
import { applyStepEvent, toPlanBlock } from './agentBridge'

describe('计划进度卡映射', () => {
  it('plan_created → 计划块，步数正确且全部 pending', () => {
    const b = toPlanBlock(7, { steps: [{ title: '读文件' }, { name: '改代码' }, { objective: '跑测试' }] })
    expect(b.id).toBe('plan:7')
    expect(b.type).toBe('plan')
    expect(b.steps.map((s) => s.title)).toEqual(['读文件', '改代码', '跑测试'])
    expect(b.steps.every((s) => s.status === 'pending')).toBe(true)
  })

  it('负载缺失不炸：无 steps → 空列表', () => {
    expect(toPlanBlock(1, undefined).steps).toEqual([])
  })

  it('步骤推进：只动目标步，其余保持', () => {
    let b = toPlanBlock(2, { steps: [{ title: 'A' }, { title: 'B' }] })
    b = applyStepEvent(b, { stepIndex: 0, status: 'running' })
    expect(b.steps[0].status).toBe('running')
    expect(b.steps[1].status).toBe('pending')
    b = applyStepEvent(b, { stepIndex: 0, status: 'done', output: '产出' })
    expect(b.steps[0].status).toBe('done')
    expect(b.steps[0].output).toBe('产出')
  })

  it('返回新对象（Vue 才看得见变化）', () => {
    const b = toPlanBlock(3, { steps: [{ title: 'A' }] })
    expect(applyStepEvent(b, { stepIndex: 0, status: 'done' })).not.toBe(b)
  })

  it('超长产出截断，避免一张卡撑爆消息区', () => {
    let b = toPlanBlock(4, { steps: [{ title: 'A' }] })
    b = applyStepEvent(b, { stepIndex: 0, status: 'done', output: 'x'.repeat(2000) })
    expect((b.steps[0].output || '').length).toBe(400)
  })
})
