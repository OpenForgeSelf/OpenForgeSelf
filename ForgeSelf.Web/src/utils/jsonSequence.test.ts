import { describe, it, expect } from 'vitest'
import { parseJsonSequence, firstJsonObject } from './jsonSequence'

describe('parseJsonSequence（流式响应容错解析）', () => {
  it('解析单个合法 JSON 对象', () => {
    const seq = parseJsonSequence('{"a":1}')
    expect(seq).toHaveLength(1)
    expect(seq[0]).toEqual({ a: 1 })
  })

  it('解析多个直接拼接的 JSON 对象（无分隔符）', () => {
    // 这是真实流式响应存储形态：}{ 相连
    const seq = parseJsonSequence('{"id":1}{"id":2}{"id":3}')
    expect(seq).toHaveLength(3)
    expect(seq.map((o) => (o as { id: number }).id)).toEqual([1, 2, 3])
  })

  it('解析含字符串内花括号的拼接对象', () => {
    const seq = parseJsonSequence('{"msg":"a}b"}{"msg":"c{d"}')
    expect(seq).toHaveLength(2)
    expect(seq[0]).toEqual({ msg: 'a}b' })
    expect(seq[1]).toEqual({ msg: 'c{d' })
  })

  it('空 / null / 非 JSON 不抛错，返回空数组', () => {
    expect(parseJsonSequence('')).toEqual([])
    expect(parseJsonSequence(null)).toEqual([])
    expect(parseJsonSequence('not json at all')).toEqual([])
  })

  it('末尾有垃圾字符时兜底整体解析', () => {
    const seq = parseJsonSequence('{"ok":true} trailing garbage')
    expect(seq).toHaveLength(1)
    expect(seq[0]).toEqual({ ok: true })
  })

  it('firstJsonObject 取首个对象', () => {
    expect(firstJsonObject('{"x":1}{"x":2}')).toEqual({ x: 1 })
    expect(firstJsonObject('')).toBeNull()
  })
})
