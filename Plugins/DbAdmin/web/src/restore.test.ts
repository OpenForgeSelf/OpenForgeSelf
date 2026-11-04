import { beforeEach, describe, expect, it } from 'vitest'
import { RESTORE_KEY, RESTORE_MAX_BYTES, buildSnapshot, byteSize, fitToBudget, loadSnapshot, saveSnapshot, stripSecrets } from './restore'
import type { DbConnection } from './types'

class MemStorage implements Storage {
  private m = new Map<string, string>()
  get length() { return this.m.size }
  clear() { this.m.clear() }
  getItem(k: string) { return this.m.get(k) ?? null }
  key(i: number) { return [...this.m.keys()][i] ?? null }
  removeItem(k: string) { this.m.delete(k) }
  setItem(k: string, v: string) { this.m.set(k, v) }
}

const conn = (id: string, extra: Partial<DbConnection> = {}): DbConnection => ({
  id, name: id, provider: 'SQLite', mode: 'form', database: `/data/${id}.db`, readonly: false, ...extra,
})

describe('S-R 刷新恢复（localStorage ≤64KB，无密码）', () => {
  let st: MemStorage
  beforeEach(() => { st = new MemStorage() })

  it('快照不含 password / connStr', () => {
    const snap = buildSnapshot([conn('a', { password: '****', connStr: 'Data Source=x;Password=****' })], 'a', { a: 'select 1' })
    const text = JSON.stringify(snap)
    expect(text).not.toMatch(/password/i)
    expect(text).not.toMatch(/connStr/i)
    expect(snap.drafts.a).toBe('select 1')
  })
  it('stripSecrets 深度剥离', () => {
    expect(stripSecrets({ a: { Password: 'x', pwd: 'y', keep: 1 }, list: [{ connStr: 'z', n: 2 }] }))
      .toEqual({ a: { keep: 1 }, list: [{ n: 2 }] })
  })
  it('保存后读回；读回时再剥一次密码', () => {
    st.setItem(RESTORE_KEY, JSON.stringify({ version: 1, savedAt: 't', activeConnectionId: 'a', connections: [{ id: 'a', name: 'a', provider: 'SQLite', mode: 'form', readonly: false, password: 'leak' }], drafts: { a: 'x' } }))
    const s = loadSnapshot(st)!
    expect(JSON.stringify(s)).not.toContain('leak')
    expect(s.activeConnectionId).toBe('a')
    expect(saveSnapshot(s, st)).toBe(true)
  })
  it('超 64KB：先丢其它连接草稿，再截断当前草稿', () => {
    const big = 'x'.repeat(40 * 1024)
    const snap = buildSnapshot([conn('a'), conn('b')], 'a', { a: big, b: big })
    expect(byteSize(JSON.stringify(snap))).toBeLessThanOrEqual(RESTORE_MAX_BYTES)
    expect(snap.drafts.b).toBeUndefined()
    expect(snap.drafts.a.length).toBe(big.length)
    const huge = fitToBudget({ ...snap, drafts: { a: 'y'.repeat(100 * 1024) } })
    expect(byteSize(JSON.stringify(huge))).toBeLessThanOrEqual(RESTORE_MAX_BYTES)
    expect(huge.drafts.a.length).toBeGreaterThan(60 * 1024)
  })
  it('丢弃已不存在连接的草稿；activeId 不存在则置空', () => {
    const snap = buildSnapshot([conn('a')], 'gone', { a: '1', gone: '2' })
    expect(snap.activeConnectionId).toBeNull()
    expect(Object.keys(snap.drafts)).toEqual(['a'])
  })
  it('超限原文被丢弃', () => {
    st.setItem(RESTORE_KEY, 'x'.repeat(RESTORE_MAX_BYTES + 1))
    expect(loadSnapshot(st)).toBeNull()
    expect(st.getItem(RESTORE_KEY)).toBeNull()
  })
})
