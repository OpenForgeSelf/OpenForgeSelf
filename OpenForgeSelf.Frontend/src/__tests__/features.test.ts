import { describe, expect, it } from 'vitest'
import { features, mergeFeatureList } from '@/data/features'
import type { PluginMenuContribution } from '@/types/plugin'

describe('mergeFeatureList', () => {
  it('清单为空时回退到内置 features', () => {
    const result = mergeFeatureList([])
    expect(result).toHaveLength(features.length)
  })

  it('已存在路由的贡献会被去重（不破坏现有界面）', () => {
    const contribution: PluginMenuContribution = {
      id: 'memory',
      name: '记忆系统插件',
      menu: '记忆',
      route: '/memory',
      icon: 'fa-brain',
      views: ['MemoryView'],
    }

    const result = mergeFeatureList([contribution])

    expect(result).toHaveLength(features.length)
  })

  it('新路由的贡献会作为补充特性追加', () => {
    const contribution: PluginMenuContribution = {
      id: 'custom',
      name: '自定义插件',
      menu: '自定义',
      route: '/custom-plugin',
      icon: 'fa-star',
      views: ['CustomView'],
    }

    const result = mergeFeatureList([contribution])

    expect(result).toHaveLength(features.length + 1)
    const added = result[result.length - 1]
    expect(added.id).toBe('custom')
    expect(added.name).toBe('自定义')
    expect(added.path).toBe('/custom-plugin')
  })
})
