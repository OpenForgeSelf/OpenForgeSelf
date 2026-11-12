import { describe, expect, it } from 'vitest'
import {
  MAX_TABS, closeTab, createManualTab, createTableTab, findTab, manualTabKey, nextManualSeq, openManualTab, openTableTab,
  replaceTab, tabTitle, tabTooltip, tableTabKey,
} from './tabs'

describe('多查询标签（FR-1~6 / FR-11）', () => {
  it('key 规则：表标签 t:<conn>:<table>，自由标签 m:<conn>:<seq>', () => {
    expect(tableTabKey('12', 'users')).toBe('t:12:users')
    expect(manualTabKey('12', 0)).toBe('m:12:0')
  })

  it('同一表重复打开只聚焦，不新建、不重置 SQL 与结果', () => {
    const a = createTableTab('12', 'users')
    a.sql = 'select * from users where id=1'
    a.results = []
    const first = openTableTab([a], '12', 'users')
    expect(first.created).toBe(false)
    expect(first.tabs).toHaveLength(1)
    expect(first.tabs[0].sql).toBe('select * from users where id=1')
    expect(first.key).toBe('t:12:users')
  })

  it('不同表 / 不同连接同名表各自独立成标签', () => {
    const t = openTableTab([], '12', 'users')
    const t2 = openTableTab(t.tabs, '12', 'orders')
    const t3 = openTableTab(t2.tabs, '99', 'users')
    expect(t3.tabs.map(x => x.key)).toEqual(['t:12:users', 't:12:orders', 't:99:users'])
  })

  it('表标签默认 SQL 只在创建时写入', () => {
    expect(createTableTab('12', 'users').sql).toBe('SELECT * FROM users')
  })

  it('上限 12：超限不新建并报 full', () => {
    let tabs = [] as ReturnType<typeof createTableTab>[]
    for (let i = 0; i < MAX_TABS; i++) tabs = openTableTab(tabs, '12', `t${i}`).tabs
    const over = openTableTab(tabs, '12', 'extra')
    expect(over.full).toBe(true)
    expect(over.key).toBeNull()
    expect(over.tabs).toHaveLength(MAX_TABS)
  })

  it('自由标签 seq 单调递增且不与已有 key 冲突', () => {
    const tabs = [createManualTab('12', 0), createManualTab('12', 2)]
    expect(nextManualSeq(tabs, '12')).toBe(3)
    expect(nextManualSeq(tabs, '99')).toBe(0)
    const r = openManualTab(tabs, '12')
    expect(r.tabs[2].key).toBe('m:12:3')
  })

  it('关闭活动标签：右邻优先，无右邻取左邻，最后一个关闭 → null', () => {
    const tabs = [createTableTab('12', 'a'), createTableTab('12', 'b'), createTableTab('12', 'c')]
    expect(closeTab(tabs, 't:12:b', 't:12:b').activeKey).toBe('t:12:c')
    expect(closeTab(tabs, 't:12:c', 't:12:c').activeKey).toBe('t:12:b')
    const last = closeTab([createTableTab('12', 'a')], 't:12:a', 't:12:a')
    expect(last.activeKey).toBeNull()
    expect(last.tabs).toHaveLength(0)
  })

  it('关闭非活动标签不改变活动标签', () => {
    const tabs = [createTableTab('12', 'a'), createTableTab('12', 'b')]
    expect(closeTab(tabs, 't:12:a', 't:12:b').activeKey).toBe('t:12:b')
  })

  it('标题不加「表」字；同名表用连接名区分 tooltip', () => {
    expect(tabTitle({ table: 'users' })).toBe('users')
    expect(tabTitle({})).toBe('新查询')
    expect(tabTooltip({ table: 'users', connectionId: '12' }, id => ({ 12: 'demo.db' } as Record<string, string>)[id] ?? id))
      .toBe('demo.db · users')
  })

  it('replaceTab 只替换目标标签且保持顺序', () => {
    const tabs = [createTableTab('12', 'a'), createTableTab('12', 'b')]
    const next = replaceTab(tabs, 't:12:b', { running: true })
    expect(next[1].running).toBe(true)
    expect(next[0]).toBe(tabs[0])
    expect(findTab(next, 't:12:b')?.running).toBe(true)
  })
})
