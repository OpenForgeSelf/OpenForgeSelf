import { describe, expect, it } from 'vitest'
import { listOf, normApproval, normConnection, normQuery, normTable, normTableDetail, normTest } from './normalize'

describe('响应归一（03 未定的 JSON 细节）', () => {
  it('query：columns:string[] + columnTypes；数字 approvalId', () => {
    const q = normQuery({ status: 'ok', columns: ['id', 'name'], columnTypes: ['INTEGER', 'TEXT'], rows: [[1, 'a']], total: 1, elapsedMs: 3 })
    expect(q.columns).toEqual([{ name: 'id', type: 'INTEGER' }, { name: 'name', type: 'TEXT' }])
    expect(normQuery({ status: 'awaiting_approval', approvalId: 42 }).approvalId).toBe('42')
  })
  it('连接：kind=system → system；密码只留掩码', () => {
    const c = normConnection({ id: 'sys:ForgeSelf', name: 'ForgeSelf', provider: 'sqlite', kind: 'system', readonly: true, password: 'anything' })
    expect(c.system).toBe(true)
    expect(c.password).toBe('****')
  })
  it('测连：ok:boolean / code=driver_missing', () => {
    expect(normTest({ ok: true, serverVersion: '3.45' }).status).toBe('ok')
    expect(normTest({ ok: false, code: 'driver_missing', message: 'x' }).status).toBe('driver_missing')
    expect(normTest({ ok: false, message: 'refused' }).status).toBe('error')
    expect(normTest({ ok: true, code: null }).code).toBe('ok')
  })
  it('kind：旧形态 destructive/drop_table → write + destructive；新形态保持', () => {
    expect(normApproval({ id: 1, kind: 'destructive' })).toMatchObject({ kind: 'write', destructive: true })
    expect(normApproval({ id: 1, kind: 'drop_table', destructive: true })).toMatchObject({ kind: 'write', destructive: true })
    expect(normApproval({ id: 1, kind: 'read' }).kind).toBe('read')
    expect(normQuery({ status: 'ok', kind: 'destructive' })).toMatchObject({ kind: 'write', destructive: true })
  })
  it('202 无 status → awaiting_approval；provider 统一小写', () => {
    expect(normQuery({ approvalId: 3 }, 202).status).toBe('awaiting_approval')
    expect(normConnection({ id: 'sys:X', provider: 'SQLite' })).toMatchObject({ provider: 'sqlite', system: true })
  })
  it('表/列：isView、primaryKeys、rawType', () => {
    expect(normTable({ name: 'v', isView: true }).type).toBe('view')
    const d = normTableDetail({ name: 't', columns: [{ name: 'id', rawType: 'INTEGER', nullable: false }], primaryKeys: ['id'] })
    expect(d.columns[0]).toEqual({ name: 'id', type: 'INTEGER', nullable: false, primaryKey: true })
  })
  it('审批 + 分页包装', () => {
    const list = listOf({ items: [{ id: 7, connectionId: '1', sqlRedacted: 'DROP TABLE t', destructive: true, status: 'pending', source: 'agent' }], total: 1 }, normApproval)
    expect(list[0]).toMatchObject({ id: '7', destructive: true, source: 'agent', status: 'pending' })
  })
})
