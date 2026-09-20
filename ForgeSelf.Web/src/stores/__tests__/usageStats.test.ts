import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useUsageStatsStore } from '@/stores/usageStats'
import { MAX_VISITS_PER_KEY } from '@/data/homeEntries'

const STORAGE_KEY = 'forge-home-usage-v2'

describe('useUsageStatsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('记录访问并按 path 持久化', () => {
    const store = useUsageStatsStore()
    store.recordVisit('/skills', 1_786_000_000_000)

    expect(store.visits['/skills']).toEqual([1_786_000_000_000])
    const raw = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}')
    expect(raw.visits['/skills']).toEqual([1_786_000_000_000])
  })

  it('忽略根路径与空路径（首页本身不算功能使用）', () => {
    const store = useUsageStatsStore()
    store.recordVisit('/')
    store.recordVisit('')
    expect(store.visits).toEqual({})
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull()
  })

  it('累计同 path 的多次访问', () => {
    const store = useUsageStatsStore()
    store.recordVisit('/chat', 1000)
    store.recordVisit('/chat', 2000)
    expect(store.visits['/chat']).toEqual([1000, 2000])
  })

  it('访问条数超过上限时只保留最近若干条', () => {
    const store = useUsageStatsStore()
    for (let i = 1; i <= MAX_VISITS_PER_KEY + 10; i++) {
      store.recordVisit('/chat', i)
    }
    expect(store.visits['/chat'].length).toBe(MAX_VISITS_PER_KEY)
    // 保留的是最近的：末值应为最大时间戳
    expect(store.visits['/chat'].at(-1)).toBe(MAX_VISITS_PER_KEY + 10)
  })

  it('固定与取消固定可切换并持久化', () => {
    const store = useUsageStatsStore()
    expect(store.isPinned('/todo')).toBe(false)

    expect(store.togglePin('/todo')).toBe(true)
    expect(store.isPinned('/todo')).toBe(true)
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!).pinned).toEqual(['/todo'])

    expect(store.togglePin('/todo')).toBe(false)
    expect(store.isPinned('/todo')).toBe(false)
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!).pinned).toEqual([])
  })

  it('能从既有 localStorage 恢复（多标签页/刷新后不丢）', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ visits: { '/memory': [1000, 2000] }, pinned: ['/todo'] }),
    )
    const store = useUsageStatsStore()
    store.load()
    expect(store.visits['/memory']).toEqual([1000, 2000])
    expect(store.isPinned('/todo')).toBe(true)
  })

  it('存储内容损坏时回退为空快照，不抛错', () => {
    localStorage.setItem(STORAGE_KEY, '{not-json')
    const store = useUsageStatsStore()
    store.load()
    expect(store.visits).toEqual({})
    expect(store.pinned).toEqual([])
  })

  it('snapshot 返回副本，外部修改不影响 store', () => {
    const store = useUsageStatsStore()
    store.recordVisit('/skills', 1000)
    store.togglePin('/todo')

    const snap = store.snapshot()
    snap.visits['/skills'].push(9999)
    snap.pinned.push('/chat')

    expect(store.visits['/skills']).toEqual([1000])
    expect(store.pinned).toEqual(['/todo'])
  })
})
