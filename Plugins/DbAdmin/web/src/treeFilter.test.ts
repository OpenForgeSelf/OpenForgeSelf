import { describe, expect, it } from 'vitest'
import { connectionVisible, databaseVisible, tableVisible, tablesCacheKey } from './treeFilter'

const sales = { name: 'sales', tables: ['orders', 'customers'] }
const mysql = { name: 'mysql', tables: ['user'] }

describe('树筛选', () => {
  it('空查询全部留下', () => {
    expect(connectionVisible('demo', [sales], [], '  ')).toBe(true)
    expect(databaseVisible(sales, '')).toBe(true)
    expect(tableVisible('orders', '', 'sales')).toBe(true)
  })

  it('连接名、库名、表名任一命中则连接留下；都不中则隐藏', () => {
    expect(connectionVisible('报表库', [sales], [], '报表')).toBe(true)
    expect(connectionVisible('报表库', [sales], [], 'sales')).toBe(true)
    expect(connectionVisible('报表库', [sales], [], 'orders')).toBe(true)
    expect(connectionVisible('报表库', [sales], [], 'nope')).toBe(false)
    expect(connectionVisible('demo.db', [], ['users'], 'users')).toBe(true)
    expect(connectionVisible('demo.db', [], ['users'], 'zzz')).toBe(false)
  })

  it('连接名命中时仍按库名/表名收紧子节点', () => {
    expect(databaseVisible(sales, '报表')).toBe(false)
    expect(databaseVisible(sales, 'orders')).toBe(true)
    expect(databaseVisible(mysql, 'orders')).toBe(false)
    expect(tableVisible('orders', '报表', 'sales')).toBe(false)
    expect(tableVisible('customers', 'sales', 'sales')).toBe(true)
    expect(tableVisible('orders', 'order', 'sales')).toBe(true)
  })

  it('无库名的缓存键就是连接 id', () => {
    expect(tablesCacheKey('1')).toBe('1')
    expect(tablesCacheKey('1', '  ')).toBe('1')
    expect(tablesCacheKey('1', 'app')).not.toBe(tablesCacheKey('1', 'other'))
  })
})
