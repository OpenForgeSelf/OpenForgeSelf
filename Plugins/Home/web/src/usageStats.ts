/**
 * 功能/插件使用度 store — 首页「常用功能」动态排序的数据底座（插件内自备副本）。
 *
 * 与宿主同源逻辑，但独立 defineStore id（homeUsageStats），避免在共享 pinia 实例上
 * 与宿主的 usageStats 注册表冲突；存储键仍用同一 localStorage 键，保证固定/热度跨页一致。
 *
 * 持久化：localStorage（先跑通闭环）。读写集中在 readStorage / writeStorage 两函数。
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { MAX_VISITS_PER_KEY, type UsageSnapshot } from './homeEntries'

const STORAGE_KEY = 'forge-home-usage-v2'

function emptySnapshot(): UsageSnapshot {
  return { visits: {}, pinned: [] }
}

/** 读取持久化快照；无数据或解析失败一律返回空快照（绝不伪造使用数据）。 */
function readStorage(): UsageSnapshot {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return emptySnapshot()
    const parsed = JSON.parse(raw) as Partial<UsageSnapshot>
    return {
      visits: parsed.visits && typeof parsed.visits === 'object' ? parsed.visits : {},
      pinned: Array.isArray(parsed.pinned) ? parsed.pinned : [],
    }
  } catch {
    return emptySnapshot()
  }
}

function writeStorage(snapshot: UsageSnapshot): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot))
  } catch {
    // 存储不可用（隐私模式/超限）时静默降级：本次会话内仍生效，只是不持久化
  }
}

export const useUsageStatsStore = defineStore('homeUsageStats', () => {
  const visits = ref<Record<string, number[]>>({})
  const pinned = ref<string[]>([])
  let loaded = false

  /** 从存储加载一次；重复调用安全。 */
  function load(): void {
    if (loaded) return
    loaded = true
    const snapshot = readStorage()
    visits.value = snapshot.visits
    pinned.value = snapshot.pinned
  }

  function persist(): void {
    writeStorage({ visits: visits.value, pinned: pinned.value })
  }

  /**
   * 记录一次功能访问。
   * @param path 路由路径（与 HomeEntry.key 对齐）
   * @param at 访问时间戳，默认当前时间；测试可注入
   */
  function recordVisit(path: string, at: number = Date.now()): void {
    if (!path || path === '/') return
    load()
    const list = visits.value[path] ?? []
    list.push(at)
    visits.value[path] = list.length > MAX_VISITS_PER_KEY ? list.slice(-MAX_VISITS_PER_KEY) : list
    persist()
  }

  function isPinned(path: string): boolean {
    load()
    return pinned.value.includes(path)
  }

  /** 切换固定状态；返回切换后是否固定。 */
  function togglePin(path: string): boolean {
    load()
    const idx = pinned.value.indexOf(path)
    if (idx >= 0) {
      pinned.value.splice(idx, 1)
    } else {
      pinned.value.push(path)
    }
    persist()
    return idx < 0
  }

  /** 取快照副本：供视图在数据更新时刻 stamped 使用，避免排序实时抖动。 */
  function snapshot(): UsageSnapshot {
    load()
    return {
      visits: JSON.parse(JSON.stringify(visits.value)) as Record<string, number[]>,
      pinned: [...pinned.value],
    }
  }

  return { visits, pinned, load, recordVisit, togglePin, isPinned, snapshot }
})
