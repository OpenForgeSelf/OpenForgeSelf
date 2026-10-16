import { describe, expect, it } from 'vitest'
import { matchTrigger, filterItems } from './utils/trigger'

/**
 * 触发判定与过滤（`/` 命令 / `@` 提及的公共逻辑）。
 *
 * 这里锁的是「什么时候弹面板」—— 弹错地方会疯狂打扰，又恰恰是最难靠肉眼回归的一段：
 *   ① 行首 / 空白后才触发（路径、邮箱、分数不能误弹）；
 *   ② 只有宿主注册过的字符才触发；
 *   ③ start 必须指向触发字符本身，否则替换时会吃掉前面的字。
 */

describe('matchTrigger — 触发判定', () => {
  it('行首输入触发字符 → 触发', () => {
    expect(matchTrigger('/', ['/', '@'])).toEqual({ char: '/', query: '', start: 0 })
  })

  it('空白后输入触发字符 → 触发', () => {
    expect(matchTrigger('帮我 ', ['/'])).toBeNull() // 空白后没有字符，不算
    expect(matchTrigger('帮我 /', ['/'])).toEqual({ char: '/', query: '', start: 3 })
  })

  it('★ 路径文本不触发（src/api/foo 中间的 /）', () => {
    expect(matchTrigger('src/api', ['/'])).toBeNull()
    expect(matchTrigger('src/api/', ['/'])).toBeNull()
  })

  it('★ 邮箱不触发（a@b.com 中间的 @）', () => {
    expect(matchTrigger('a@b', ['@'])).toBeNull()
    expect(matchTrigger('联系 a@b.com ', ['@'])).toBeNull()
  })

  it('分数 / 日期中间的斜杠不触发', () => {
    expect(matchTrigger('1/2', ['/'])).toBeNull()
    expect(matchTrigger('2026/10/15', ['/'])).toBeNull()
  })

  it('触发字符后有查询词：query 与 start 都要对', () => {
    expect(matchTrigger('/read', ['/'])).toEqual({ char: '/', query: 'read', start: 0 })
    expect(matchTrigger('用 /read', ['/'])).toEqual({ char: '/', query: 'read', start: 2 })
  })

  it('未注册的字符不触发（宿主只要 / 时，@ 不弹）', () => {
    expect(matchTrigger('@', ['/'])).toBeNull()
  })

  it('空文本 / 空字符表 → 不触发', () => {
    expect(matchTrigger('', ['/'])).toBeNull()
    expect(matchTrigger('/', [])).toBeNull()
  })

  it('★ start 指向触发字符：替换区间 [start, 光标) 正好覆盖 "/query"', () => {
    const before = '帮我 /read'
    const hit = matchTrigger(before, ['/'])!
    // 用 start 与 query 去替换，应恰好删掉触发段、保留前面的「帮我 」
    const replaced = before.slice(0, hit.start) + 'X' + before.slice(hit.start + 1 + hit.query.length)
    expect(replaced).toBe('帮我 X')
  })
})

describe('filterItems — 候选项过滤', () => {
  const items = [
    { name: 'read_file', desc: '读取文件内容', meta: 'FileTools' },
    { name: 'write_file', desc: '写入文件', meta: 'FileTools' },
    { name: 'run_terminal_command', desc: '执行命令', meta: 'ai-agent' },
  ]

  it('空 query 返回全部', () => {
    expect(filterItems(items, '')).toHaveLength(3)
  })

  it('按名称匹配', () => {
    expect(filterItems(items, 'read').map((i) => i.name)).toEqual(['read_file'])
  })

  it('按描述匹配（用户搜「命令」也能找到 run_terminal_command）', () => {
    expect(filterItems(items, '命令').map((i) => i.name)).toEqual(['run_terminal_command'])
  })

  it('按 meta（插件 id）匹配', () => {
    expect(filterItems(items, 'FileTools')).toHaveLength(2)
  })

  it('无匹配返回空数组，不抛错', () => {
    expect(filterItems(items, 'zzz')).toEqual([])
    expect(filterItems(items, 'zzz', 5)).toEqual([])
  })

  it('limit 生效（工具 112 个不能一次性铺满）', () => {
    expect(filterItems(items, '', 2)).toHaveLength(2)
  })

  it('非数组入参不炸', () => {
    expect(filterItems(undefined as never, 'a')).toEqual([])
  })
})
