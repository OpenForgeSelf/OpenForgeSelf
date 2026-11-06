import { describe, expect, it } from 'vitest'
import { csvCell, csvField, csvFileName, csvWithBom, rowText, safeBaseName, timeCode, toCsv } from './exportCsv'

describe('整行复制（FR-21 / AC-22）', () => {
  it('制表符分隔，NULL 写 NULL', () => {
    expect(rowText([1, 'user_001', null, undefined])).toBe('1\tuser_001\tNULL\tNULL')
  })
  it('对象值按 JSON 序列化', () => {
    expect(rowText([{ a: 1 }, 'x,y'])).toBe('{"a":1}\tx,y')
  })
})

describe('CSV 导出（FR-21 / BR-21 / AC-23）', () => {
  it('RFC 4180 转义：逗号/双引号/换行', () => {
    expect(csvField('plain')).toBe('plain')
    expect(csvField('a,b')).toBe('"a,b"')
    expect(csvField('say "hi"')).toBe('"say ""hi"""')
    expect(csvField('line1\nline2')).toBe('"line1\nline2"')
  })

  it('NULL 写 NULL；对象序列化', () => {
    expect(csvCell(null)).toBe('NULL')
    expect(csvCell(undefined)).toBe('NULL')
    expect(csvCell({ a: 1 })).toBe('{"a":1}')
    expect(csvCell(0)).toBe('0')
    expect(csvCell(false)).toBe('false')
  })

  it('含表头 + 数据行，CRLF 分隔', () => {
    const text = toCsv([{ name: 'id' }, { name: 'name' }], [[1, 'a,b'], [2, null]])
    expect(text).toBe('id,name\r\n1,"a,b"\r\n2,NULL\r\n')
  })

  it('UTF-8 BOM', () => {
    expect(csvWithBom('a,b\r\n')).toBe('\uFEFFa,b\r\n')
  })

  it('文件名含结果序号与页码 + yyMMddHHmm', () => {
    const now = new Date(2026, 10, 6, 9, 5)
    expect(csvFileName('users', 2, 3, now)).toBe('users-r2-p3-2611060905.csv')
    expect(timeCode(now)).toBe('2611060905')
  })

  it('文件名非法字符替换', () => {
    expect(safeBaseName('a/b:c d')).toBe('a_b_c_d')
    expect(safeBaseName('   ')).toBe('query')
  })
})
