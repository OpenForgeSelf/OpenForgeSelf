import { describe, expect, it, vi } from 'vitest'
import { classifyQueryResponse, errorReason, orchestrateApprove, rejectRequest } from './approvalFlow'

describe('审批 → 破坏性二次确认编排', () => {
  it('普通写：批准即 resolve，不问二次确认', async () => {
    const confirm = vi.fn(async () => true)
    const r = await orchestrateApprove({ destructive: false }, confirm)
    expect(r).toEqual({ action: 'resolve', request: { decision: 'approve' } })
    expect(confirm).not.toHaveBeenCalled()
  })
  it('破坏性：批准后二次确认通过 → confirmDestructive=true', async () => {
    const confirm = vi.fn(async () => true)
    const r = await orchestrateApprove({ destructive: true }, confirm)
    expect(confirm).toHaveBeenCalledTimes(1)
    expect(r).toEqual({ action: 'resolve', request: { decision: 'approve', confirmDestructive: true } })
  })
  it('破坏性：二次确认取消 → 不发 resolve（审批保持 pending）', async () => {
    const r = await orchestrateApprove({ destructive: true }, async () => false)
    expect(r).toEqual({ action: 'cancelled' })
  })
  it('拒绝：decision=reject 且不带 confirmDestructive', () => {
    expect(rejectRequest()).toEqual({ decision: 'reject' })
  })
  it('query 响应分流：硬拒/只读写入 → 红条，不弹审批', () => {
    expect(classifyQueryResponse({ status: 'rejected', code: 'rejected_hard' })).toBe('red_bar')
    expect(classifyQueryResponse({ status: 'rejected', code: 'readonly_connection' })).toBe('red_bar')
    expect(classifyQueryResponse({ status: 'awaiting_approval', approvalId: 'a1' })).toBe('open_approval')
    expect(classifyQueryResponse({ status: 'ok', rows: [] })).toBe('show_result')
  })
  it('错误码中文原因', () => {
    expect(errorReason('readonly_connection')).toContain('只读连接')
    expect(errorReason('sql_error', 'near "SELEC"')).toBe('SQL 错误 · near "SELEC"')
    expect(errorReason('weird')).toBe('执行失败')
  })
})
