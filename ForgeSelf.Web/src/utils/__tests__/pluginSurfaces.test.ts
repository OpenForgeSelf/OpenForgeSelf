import { describe, expect, it } from 'vitest'
import type { FrontendContributes } from '@/types/plugin'
import {
  hasSurface,
  KNOWN_PLUGIN_SURFACES,
  normalizeSurfaces,
  resolveSurfaces,
} from '../pluginSurfaces'

describe('pluginSurfaces 规范化', () => {
  it('KNOWN_PLUGIN_SURFACES 含四类规范值', () => {
    expect(KNOWN_PLUGIN_SURFACES).toEqual(['tab', 'home-widget', 'quick-action', 'status-badge'])
  })

  it('normalizeSurfaces trim + 小写化 + 去重 + 丢未知', () => {
    expect(normalizeSurfaces(['TAB', ' Home-Widget ', 'tab', 'quick-action', 'side-drawer', '', '  ']))
      .toEqual(['tab', 'home-widget', 'quick-action'])
    expect(normalizeSurfaces(undefined)).toEqual([])
    expect(normalizeSurfaces(null)).toEqual([])
    expect(normalizeSurfaces([])).toEqual([])
  })

  it('resolveSurfaces: 显式声明优先', () => {
    const fe: FrontendContributes = {
      views: ['HomeView'],
      route: '/home',
      surfaces: ['home-widget', 'status-badge'],
    }
    expect(resolveSurfaces(fe)).toEqual(['home-widget', 'status-badge'])
  })

  it('resolveSurfaces: 旧插件无 surfaces 但带 route+views 视为 tab（向后兼容）', () => {
    const legacy: FrontendContributes = { views: ['MemoryView'], route: '/memory', menu: '记忆' }
    expect(resolveSurfaces(legacy)).toEqual(['tab'])

    // 无 route 或无 views → 空（不默认 tab）
    expect(resolveSurfaces({ views: ['X'] })).toEqual([])
    expect(resolveSurfaces({ route: '/x', views: [] })).toEqual([])
    expect(resolveSurfaces(null)).toEqual([])
    expect(resolveSurfaces(undefined)).toEqual([])
  })

  it('hasSurface: 按归一结果判断', () => {
    const fe: FrontendContributes = { views: ['V'], route: '/v', surfaces: ['home-widget'] }
    expect(hasSurface(fe, 'home-widget')).toBe(true)
    expect(hasSurface(fe, 'tab')).toBe(false)

    const legacy: FrontendContributes = { views: ['V'], route: '/v' }
    expect(hasSurface(legacy, 'tab')).toBe(true)
    expect(hasSurface(legacy, 'quick-action')).toBe(false)
  })
})
