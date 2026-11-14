import { describe, expect, it } from 'vitest'
import { csvFromPages, exportFileName, shouldStopAfterPage, toInsert, toJson } from './exportResult'

const cols = [{ name: 'id' }, { name: 'name' }]

describe('导出全部与当前页 JSON/INSERT', () => {
  it('多页拼成一份 CSV，表头只出现一次', () => {
    const text = csvFromPages(cols, [[[1, 'a']], [[2, null]]])
    expect(text).toBe('id,name\r\n1,a\r\n2,NULL\r\n')
  })

  it('总行数已知则到齐即停；未知时本页不满即停；硬顶 50 页', () => {
    expect(shouldStopAfterPage(2, 50, 50, 120)).toBe(false)
    expect(shouldStopAfterPage(3, 50, 20, 120)).toBe(true)
    expect(shouldStopAfterPage(1, 50, 10, undefined)).toBe(true)
    expect(shouldStopAfterPage(1, 50, 50, undefined)).toBe(false)
    expect(shouldStopAfterPage(50, 50, 50, undefined)).toBe(true)
  })

  it('JSON 为对象数组，NULL 为 null', () => {
    expect(JSON.parse(toJson(cols, [[1, null], [2, undefined]]))).toEqual([
      { id: 1, name: null },
      { id: 2, name: null },
    ])
  })

  it('INSERT 每行一条，字符串单引号加倍', () => {
    expect(toInsert('users', cols, [[1, "O'Brien"]])).toBe("INSERT INTO users (id, name) VALUES (1, 'O''Brien');\n")
    expect(toInsert('odd name', [{ name: 'a b' }], [[null]])).toBe('INSERT INTO "odd name" ("a b") VALUES (NULL);\n')
  })

  it('文件名页码段可以是 all', () => {
    const now = new Date(2026, 10, 14, 8, 1)
    expect(exportFileName('users', 1, 'all', 'csv', now)).toBe('users-r1-pall-2611140801.csv')
    expect(exportFileName('users', 1, 2, 'json', now)).toBe('users-r1-p2-2611140801.json')
  })
})
