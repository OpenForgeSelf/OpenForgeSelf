/**
 * 场景/页面注册表与设备框（03-plan §U 标签即判据 + AC15 可测部分）。
 * 标签在这里被钉死：改标签会让 e2e 的 `getByRole('tab', { name })` 判据失效，必须先改契约。
 */
import { describe, expect, it } from 'vitest'

import { DEVICES, SCENES, deviceById, findPage, firstPage, forcesMobile, sceneById } from './scenes'

describe('scenes 注册表（场景/页面/设备为唯一真源）', () => {
  it('场景页签名恰为契约五类（A 片 admin/board + B 片 workbench/landing/mobile）', () => {
    expect(SCENES.map((s) => s.id)).toEqual(['admin', 'board', 'workbench', 'landing', 'mobile'])
    expect(SCENES.map((s) => s.label)).toEqual(['后台/中台', '状态板', '工具/工作台', '官网/落地页', '移动端 H5'])
  })

  it('B 片三场景各一个页面，标签/component 键登记齐全', () => {
    expect(sceneById('workbench')?.pages.map((p) => [p.id, p.label, p.component])).toEqual([
      ['workbench-editor', '编辑器', 'WorkbenchEditor'],
    ])
    expect(sceneById('landing')?.pages.map((p) => [p.id, p.label, p.component])).toEqual([
      ['landing-home', '首页', 'LandingHome'],
    ])
    expect(sceneById('mobile')?.pages.map((p) => [p.id, p.label, p.component])).toEqual([
      ['mobile-home', '首页', 'MobileHome'],
    ])
  })

  it('mobile 场景强制手机设备框（AC15）', () => {
    expect(sceneById('mobile')?.device).toBe('mobile')
    expect(forcesMobile(sceneById('mobile')!)).toBe(true)
    expect(forcesMobile(sceneById('admin')!)).toBe(false)
  })

  it('admin 场景的页面页签名恰为契约五个', () => {
    const admin = sceneById('admin')
    expect(admin).not.toBeNull()
    expect(admin?.pages.map((p) => p.label)).toEqual(['仪表盘', '列表', '表单', '详情', '设置'])
    expect(admin?.pages.map((p) => p.id)).toEqual([
      'admin-dashboard',
      'admin-list',
      'admin-form',
      'admin-detail',
      'admin-settings',
    ])
  })

  it('设备框三档宽度 = 1280/820/390（AC15）', () => {
    expect(DEVICES.map((d) => [d.id, d.width])).toEqual([
      ['desktop', 1280],
      ['tablet', 820],
      ['mobile', 390],
    ])
    expect(deviceById('mobile')?.width).toBe(390)
    expect(deviceById('nope')).toBeNull()
  })

  it('页 id 全局唯一，且每个页面都登记了 component 键', () => {
    const ids = SCENES.flatMap((s) => s.pages.map((p) => p.id))
    expect(new Set(ids).size).toBe(ids.length)
    for (const s of SCENES) for (const p of s.pages) expect(p.component).not.toBe('')
  })

  it('findPage 命中已知页、未知页回 null（深链不编造落点）', () => {
    const hit = findPage('admin-form')
    expect(hit?.scene.id).toBe('admin')
    expect(hit?.page.label).toBe('表单')
    expect(findPage('does-not-exist')).toBeNull()
  })

  it('firstPage 是场景的第一个页面（切场景落点）', () => {
    expect(firstPage(sceneById('board')!).id).toBe('status-board')
  })
})