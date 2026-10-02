/**
 * 深链解析单测（FR14 / AC22）。
 *
 * 判据三件事：① 合法哈希解析正确（含 workbench sub / showroom 页面与三个查询项）；
 * ② 非法一律"忽略"而非"猜"——非法 mode 整条作废回 null，非法 sub/查询项只丢该项、保 mode；
 * ③ `parseHash`/`formatHash` 往返一致（可分享、可书签的根基）。
 */
import { describe, expect, it } from 'vitest'
import { formatHash, parseHash, type RouteState } from './route'

describe('parseHash：合法哈希', () => {
  it('只给 mode', () => {
    expect(parseHash('#/delivery')).toEqual({ mode: 'delivery' })
    expect(parseHash('#/start')).toEqual({ mode: 'start' })
  })

  it('workbench 的 sub = section key', () => {
    expect(parseHash('#/workbench/projects')).toEqual({ mode: 'workbench', sub: 'projects' })
    expect(parseHash('#/workbench/export')).toEqual({ mode: 'workbench', sub: 'export' })
  })

  it('showroom 的 sub = 页面 id，且带 outfit/theme/device', () => {
    expect(parseHash('#/showroom/admin-dashboard?outfit=preset:admin-calm&theme=dark&device=mobile')).toEqual({
      mode: 'showroom',
      sub: 'admin-dashboard',
      outfit: 'preset:admin-calm',
      theme: 'dark',
      device: 'mobile',
    })
  })

  it('查询项顺序无关', () => {
    expect(parseHash('#/showroom/status-board?theme=light&outfit=tuned:1')).toEqual({
      mode: 'showroom',
      sub: 'status-board',
      outfit: 'tuned:1',
      theme: 'light',
    })
  })
})

describe('parseHash：非法输入', () => {
  it('非法 mode → null（整条作废，回落默认规则）', () => {
    expect(parseHash('')).toBe(null)
    expect(parseHash('#')).toBe(null)
    expect(parseHash('#/')).toBe(null)
    expect(parseHash('#/nope')).toBe(null)
    expect(parseHash('not-a-hash')).toBe(null)
    expect(parseHash('#/Settings')).toBe(null)
  })

  it('非法 sub 只丢 sub、保 mode', () => {
    expect(parseHash('#/workbench/nope')).toEqual({ mode: 'workbench' })
    expect(parseHash('#/showroom/nope')).toEqual({ mode: 'showroom' })
    // start / delivery 不接受 sub
    expect(parseHash('#/delivery/projects')).toEqual({ mode: 'delivery' })
    expect(parseHash('#/start/projects')).toEqual({ mode: 'start' })
  })

  it('非法查询项只丢该项、保 mode 与 sub', () => {
    expect(parseHash('#/showroom/admin-dashboard?outfit=BAD!&device=watch&theme=')).toEqual({
      mode: 'showroom',
      sub: 'admin-dashboard',
    })
    expect(parseHash('#/showroom/admin-dashboard?outfit=has space')).toEqual({
      mode: 'showroom',
      sub: 'admin-dashboard',
    })
  })
})

describe('formatHash', () => {
  it('只给 mode', () => {
    expect(formatHash({ mode: 'delivery' })).toBe('#/delivery')
    expect(formatHash({ mode: 'start' })).toBe('#/start')
  })

  it('带 sub 与查询项（固定顺序 outfit → theme → device）', () => {
    expect(formatHash({ mode: 'workbench', sub: 'projects' })).toBe('#/workbench/projects')
    expect(
      formatHash({ mode: 'showroom', sub: 'admin-dashboard', outfit: 'preset:admin-calm', theme: 'dark', device: 'mobile' }),
    ).toBe('#/showroom/admin-dashboard?outfit=preset:admin-calm&theme=dark&device=mobile')
  })
})

describe('往返一致（AC22）', () => {
  const HASHES = [
    '#/start',
    '#/delivery',
    '#/workbench/projects',
    '#/workbench/export',
    '#/showroom/status-board',
    '#/showroom/admin-dashboard?outfit=preset:admin-calm&theme=dark&device=mobile',
    '#/showroom/landing-home?outfit=tuned:2&theme=light',
  ]

  it.each(HASHES)('%s：parseHash → formatHash 逐字还原', (h) => {
    const state = parseHash(h)
    expect(state).not.toBe(null)
    expect(formatHash(state as RouteState)).toBe(h)
  })

  it('formatHash → parseHash 得到同一状态', () => {
    const state: RouteState = { mode: 'showroom', sub: 'mobile-home', outfit: 'project:demo', theme: 'brand', device: 'mobile' }
    expect(parseHash(formatHash(state))).toEqual(state)
  })
})