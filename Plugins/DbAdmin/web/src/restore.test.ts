import { beforeEach, describe, expect, it } from 'vitest'
import { RESTORE_KEY, RESTORE_MAX_BYTES, buildSnapshot, byteSize, fitToBudget, loadSnapshot, saveSnapshot, stripSecrets } from './restore'
import type { DbConnection } from './types'
import type { RestoredTab } from './restore'

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

const tab = (connectionId: string, table: string | undefined, sql: string): RestoredTab => ({
  key: table ? `t:${connectionId}:${table}` : `m:${connectionId}:0`, connectionId, table, sql,
})

describe('S-R 刷新恢复（localStorage ≤64KB，无密码；标签口径）', () => {
  let st: MemStorage
  beforeEach(() => { st = new MemStorage() })

  it('快照不含 password / connStr；保留标签 SQL 与默认表', () => {
    const snap = buildSnapshot(
      [conn('a', { password: '****', connStr: 'Data Source=x;Password=****' })],
      [tab('a', 'users', 'select 1')],
      't:a:users',
      { a: 'users' },
    )
    const text = JSON.stringify(snap)
    expect(text).not.toMatch(/password/i)
    expect(text).not.toMatch(/connStr/i)
    expect(snap.tabs[0].sql).toBe('select 1')
    expect(snap.activeTabKey).toBe('t:a:users')
    expect(snap.defaultTables.a).toBe('users')
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
    expect(s.tabs[0].key).toBe('m:a:0')
    expect(s.tabs[0].sql).toBe('x')
    expect(saveSnapshot(s, st)).toBe(true)
  })

  it('旧快照 drafts 迁移为每连接一个自由标签（BR-8）', () => {
    st.setItem(RESTORE_KEY, JSON.stringify({
      version: 1, savedAt: 't', activeConnectionId: 'b',
      connections: [{ id: 'a', name: 'a', provider: 'sqlite', mode: 'form', readonly: false }, { id: 'b', name: 'b', provider: 'sqlite', mode: 'form', readonly: false }],
      drafts: { a: 'select a', b: 'select b' },
    }))
    const s = loadSnapshot(st)!
    expect(s.tabs.map(t => t.key)).toEqual(['m:a:0', 'm:b:0'])
    expect(s.tabs[1].sql).toBe('select b')
    expect(s.defaultTables).toEqual({})
  })

  it('新快照往返一致（标签顺序/活动标签/默认表）', () => {
    const snap = buildSnapshot([conn('a')], [tab('a', 'users', 'select 1'), tab('a', 'orders', 'select 2')], 't:a:orders', { a: 'orders' })
    expect(saveSnapshot(snap, st)).toBe(true)
    const back = loadSnapshot(st)!
    expect(back.tabs.map(t => t.key)).toEqual(['t:a:users', 't:a:orders'])
    expect(back.tabs[1].sql).toBe('select 2')
    expect(back.activeTabKey).toBe('t:a:orders')
    expect(back.defaultTables).toEqual({ a: 'orders' })
  })

  it('超 64KB：先丢非活动标签 SQL，再截断活动标签 SQL', () => {
    const big = 'x'.repeat(40 * 1024)
    const snap = buildSnapshot([conn('a'), conn('b')], [tab('a', 't1', big), tab('a', 't2', big)], 't:a:t2', {})
    expect(byteSize(JSON.stringify(snap))).toBeLessThanOrEqual(RESTORE_MAX_BYTES)
    expect(snap.tabs[0].sql).toBe('')
    expect(snap.tabs[1].sql.length).toBe(big.length)
    const huge = fitToBudget({ ...snap, tabs: [tab('a', 't1', ''), tab('a', 't2', 'y'.repeat(100 * 1024))], activeTabKey: 't:a:t2' })
    expect(byteSize(JSON.stringify(huge))).toBeLessThanOrEqual(RESTORE_MAX_BYTES)
    expect(huge.tabs[1].sql.length).toBeGreaterThan(60 * 1024)
  })

  it('丢弃已不存在连接的标签；活动标签不存在则回退首个', () => {
    const snap = buildSnapshot([conn('a')], [tab('a', 'users', '1'), tab('gone', 'x', '2')], 't:gone:x', {})
    expect(snap.tabs.map(t => t.key)).toEqual(['t:a:users'])
    expect(snap.activeTabKey).toBe('t:a:users')
    expect(snap.defaultTables).toEqual({})
  })

  it('默认表 trim 后为空 = 未配置', () => {
    const snap = buildSnapshot([conn('a')], [], null, { a: '   ', b: 'users' })
    expect(snap.defaultTables).toEqual({})
  })

  it('超限原文被丢弃', () => {
    st.setItem(RESTORE_KEY, 'x'.repeat(RESTORE_MAX_BYTES + 1))
    expect(loadSnapshot(st)).toBeNull()
    expect(st.getItem(RESTORE_KEY)).toBeNull()
  })
})
