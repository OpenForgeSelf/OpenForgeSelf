import { defineComponent, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import AskPanel from './agent-core/components/AskPanel.vue'
import {
  approvalFeedback,
  fetchSessionApprovals,
  mapPendingApprovals,
  shouldPollApprovals,
  submitApprovalDecision,
  type ToolApproval,
} from './approvalAsk'

const FULL_COMMAND = 'Remove-Item -Recurse -Force D:\\secret\\keep-this-tail-XXXX'

function pendingRow(over: Partial<ToolApproval> = {}): ToolApproval {
  return {
    id: 'ap-1',
    sessionId: 'sess-1',
    toolName: 'run_terminal_command',
    argsJson: JSON.stringify({ command: FULL_COMMAND, cwd: 'D:\\src\\my-proj' }),
    argsHash: 'hash-1',
    riskLevel: 'high',
    description: '清理目录',
    status: 'Pending',
    createdAt: '2026-11-02T07:00:00Z',
    expiresAt: '2026-11-02T07:05:00Z',
    ...over,
  }
}

/** 与 AiAgentView 同一条接线：Pending → AskPanel，点批准/拒绝打 decide。 */
const Harness = defineComponent({
  components: { AskPanel },
  props: {
    seed: { type: Array, required: true },
    post: { type: Function, required: true },
  },
  setup(props) {
    const items = ref(props.seed as ReturnType<typeof mapPendingApprovals>)
    const feedback = ref('')
    const errorText = ref('')
    async function onResolve(e: { id: string; decision: string }) {
      try {
        await submitApprovalDecision(e.id, e.decision, props.post as (path: string, body: unknown) => Promise<unknown>)
        items.value = items.value.filter((i) => i.id !== e.id)
        feedback.value = approvalFeedback(e.decision)
      } catch (err) {
        errorText.value = err instanceof Error ? err.message : String(err)
      }
    }
    return { items, feedback, errorText, onResolve }
  },
  template: `
    <AskPanel :items="items" @resolve="onResolve" />
    <p class="fb">{{ feedback }}</p>
    <p class="err">{{ errorText }}</p>
  `,
})

describe('会话审批卡', () => {
  it('有 pending 则出卡，点批准会请求 decide，成功后卡消失', async () => {
    const post = vi.fn().mockResolvedValue(undefined)
    const items = mapPendingApprovals([pendingRow()], 'sess-1')
    const w = mount(Harness, { props: { seed: items, post } })

    expect(w.find('.aiw-ask').exists()).toBe(true)
    expect(w.get('.aiw-ask__title').text()).toBe('run_terminal_command')
    expect(w.get('.aiw-ask__badge').text()).toBe('高风险')
    expect(w.get('.aiw-ask__detail').text()).toBe('清理目录')
    const args = w.get('.aiw-ask__args').text()
    expect(args).toBe(items[0].argsText)
    expect(args).toContain('keep-this-tail-XXXX')
    expect(args).toContain('"cwd"')
    expect(args).toContain('my-proj')
    expect(args).not.toMatch(/…|\.\.\./)

    await w.get('.aiw-ask__btn--ok').trigger('click')
    await vi.waitFor(() => expect(w.find('.aiw-ask').exists()).toBe(false))

    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/api/ai-agent/approvals/ap-1/decide', { approved: true })
    expect(w.get('.fb').text()).toBe('已批准')
  })

  it('点拒绝提交 approved:false；失败时卡还在并显示错误', async () => {
    const post = vi.fn().mockRejectedValue(new Error('请求失败(409): 审批已过期'))
    const w = mount(Harness, { props: { seed: mapPendingApprovals([pendingRow()]), post } })
    const buttons = w.findAll('.aiw-ask__btn')
    await buttons[1].trigger('click')
    await vi.waitFor(() => expect(w.get('.err').text()).toContain('审批已过期'))

    expect(post).toHaveBeenCalledWith('/api/ai-agent/approvals/ap-1/decide', { approved: false })
    expect(w.find('.aiw-ask').exists()).toBe(true)
    expect(w.get('.fb').text()).toBe('')
  })

  it('非 Pending 不出卡；参数原文不被截断', () => {
    const longCmd = `echo ${'A'.repeat(800)}`
    const items = mapPendingApprovals([
      pendingRow({ id: 'done', status: 'Approved', argsJson: longCmd }),
      pendingRow({ id: 'no', status: 'Denied' }),
      pendingRow({ id: 'old', status: 'Expired' }),
      pendingRow({ id: 'stop', status: 'Cancelled' }),
      pendingRow({ id: 'other', sessionId: 'sess-2' }),
      pendingRow({ id: 'live', status: 'pending', argsJson: longCmd, description: '' }),
    ], 'sess-1')
    expect(items.map((i) => i.id)).toEqual(['live'])
    expect(items[0].argsText).toBe(longCmd)
    expect(items[0].argsText?.length).toBe(longCmd.length)
    const w = mount(AskPanel, { props: { items } })
    expect(w.get('.aiw-ask__args').text()).toBe(longCmd)
  })

  it('按 sessionId 拉列表，只保留 Pending', async () => {
    const get = vi.fn().mockResolvedValue({
      items: [
        pendingRow({ sessionId: 'sess 1' }),
        pendingRow({ id: 'gone', status: 'Denied', sessionId: 'sess 1' }),
      ],
    })
    const items = await fetchSessionApprovals('sess 1', get)
    expect(get).toHaveBeenCalledWith('/api/ai-agent/approvals?sessionId=sess%201')
    expect(items.map((i) => i.id)).toEqual(['ap-1'])
  })

  it('进行中或已有待批卡才轮询', () => {
    expect(shouldPollApprovals(true, 0)).toBe(true)
    expect(shouldPollApprovals(false, 1)).toBe(true)
    expect(shouldPollApprovals(false, 0)).toBe(false)
  })
})
