import { describe, it, expect } from 'vitest'
import {
  HOME_ENTRY_LIMIT,
  MAX_VISITS_PER_KEY,
  buildHomeEntries,
  rankEntries,
  rankEntriesDetailed,
  usageScore,
  type UsageSnapshot,
} from '@/data/homeEntries'
import type { FeatureItem } from '@/data/features'

/** 测试基准时刻：2026-09-19T00:00:00Z */
const NOW = 1_786_000_000_000
const DAY = 86_400_000

function feature(path: string | null, overrides: Partial<FeatureItem> = {}): FeatureItem {
  return {
    id: path ?? 'no-path',
    name: overrides.name ?? path ?? '无名功能',
    icon: 'package',
    category: 'tools',
    categoryLabel: '工具',
    color: 'var(--el-color-info)',
    bgColor: 'transparent',
    description: '',
    stats: '',
    enabled: true,
    path,
    signals: {},
    ...overrides,
  }
}

function usage(visits: Record<string, number[]>, pinned: string[] = []): UsageSnapshot {
  return { visits, pinned }
}

describe('buildHomeEntries', () => {
  it('把有路径的功能派生为首页入口，key 等于路由 path', () => {
    const entries = buildHomeEntries([feature('/ai-agent', { name: 'AI Agent' })])
    expect(entries).toEqual([{ key: '/ai-agent', label: 'AI Agent', icon: 'package', path: '/ai-agent' }])
  })

  it('过滤无页面的功能（如 scheduler 的 path 为 null）', () => {
    const entries = buildHomeEntries([feature(null, { name: '定时任务' }), feature('/chat')])
    expect(entries.map((e) => e.key)).toEqual(['/chat'])
  })

  it('过滤未启用的功能', () => {
    const entries = buildHomeEntries([feature('/a', { enabled: false }), feature('/b')])
    expect(entries.map((e) => e.key)).toEqual(['/b'])
  })
})

describe('usageScore', () => {
  it('无访问记录为 0 分', () => {
    expect(usageScore(undefined, NOW)).toBe(0)
    expect(usageScore([], NOW)).toBe(0)
  })

  it('刚刚访问一次约 1 分', () => {
    expect(usageScore([NOW], NOW)).toBeCloseTo(1, 5)
  })

  it('经过一个半衰期（7 天）权重减半', () => {
    expect(usageScore([NOW - 7 * DAY], NOW)).toBeCloseTo(0.5, 5)
  })

  it('高频访问高于单次访问', () => {
    const many = Array.from({ length: 5 }, () => NOW)
    expect(usageScore(many, NOW)).toBeGreaterThan(usageScore([NOW], NOW))
  })

  it('未来时间戳不计入（防时钟回拨刷分）', () => {
    expect(usageScore([NOW + DAY], NOW)).toBe(0)
  })

  it('超过 8 个半衰期的访问不再计分，旧热点不会永久霸榜', () => {
    expect(usageScore([NOW - 60 * DAY], NOW)).toBe(0)
  })

  it('近期的高频访问能压过陈旧的同等次数访问', () => {
    const fresh = Array.from({ length: 3 }, () => NOW - 1 * DAY)
    const stale = Array.from({ length: 3 }, () => NOW - 30 * DAY)
    expect(usageScore(fresh, NOW)).toBeGreaterThan(usageScore(stale, NOW))
  })
})

describe('rankEntries', () => {
  const features = [
    feature('/ai-agent', { name: 'AI Agent' }),
    feature('/chat', { name: '核心聊天' }),
    feature('/skills', { name: '技能' }),
  ]

  it('冷启动（无使用数据）回退 features.ts 原始顺序', () => {
    const ranked = rankEntries(buildHomeEntries(features), usage({}), NOW)
    expect(ranked.map((e) => e.key)).toEqual(['/ai-agent', '/chat', '/skills'])
  })

  it('使用频率高的排在前面', () => {
    const snapshot = usage({
      '/ai-agent': [NOW - 10 * DAY],
      '/skills': [NOW, NOW, NOW],
    })
    const ranked = rankEntries(buildHomeEntries(features), snapshot, NOW)
    expect(ranked[0].key).toBe('/skills')
  })

  it('固定的功能排在最前，即使从未使用过', () => {
    const snapshot = usage({ '/ai-agent': [NOW, NOW, NOW, NOW] }, ['/skills'])
    const ranked = rankEntries(buildHomeEntries(features), snapshot, NOW)
    expect(ranked[0].key).toBe('/skills')
  })

  it('多个固定项之间仍按热度排序', () => {
    const snapshot = usage({ '/chat': [NOW, NOW], '/skills': [NOW] }, ['/skills', '/chat'])
    const ranked = rankEntries(buildHomeEntries(features), snapshot, NOW)
    expect(ranked.map((e) => e.key)).toEqual(['/chat', '/skills', '/ai-agent'])
  })

  it('分数相同时保持原始顺序（排序稳定，不抖动）', () => {
    const snapshot = usage({ '/chat': [NOW], '/skills': [NOW] })
    const ranked = rankEntries(buildHomeEntries(features), snapshot, NOW)
    expect(ranked.map((e) => e.key)).toEqual(['/chat', '/skills', '/ai-agent'])
  })

  it('默认只取前 HOME_ENTRY_LIMIT 条', () => {
    const many = Array.from({ length: 30 }, (_, i) => feature(`/p${i}`, { name: `功能${i}` }))
    const ranked = rankEntries(buildHomeEntries(many), usage({}), NOW)
    expect(ranked.length).toBe(HOME_ENTRY_LIMIT)
    expect(ranked[0].key).toBe('/p0')
  })

  it('详细结果携带 score 与 pinned，供 UI 标记', () => {
    const snapshot = usage({ '/skills': [NOW] }, ['/chat'])
    const detailed = rankEntriesDetailed(buildHomeEntries(features), snapshot, NOW)
    expect(detailed[0]).toMatchObject({ pinned: true })
    expect(detailed[0].entry.key).toBe('/chat')
    expect(detailed.find((d) => d.entry.key === '/skills')!.score).toBeCloseTo(1, 5)
    expect(detailed.find((d) => d.entry.key === '/ai-agent')!.score).toBe(0)
  })
})

describe('存储体积约束', () => {
  it('单入口访问上限常量存在且合理，避免 localStorage 膨胀', () => {
    expect(MAX_VISITS_PER_KEY).toBeGreaterThan(0)
    expect(MAX_VISITS_PER_KEY).toBeLessThanOrEqual(100)
  })
})
