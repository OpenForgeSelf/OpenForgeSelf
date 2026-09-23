import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface NavTab {
  key: string
  label: string
  path: string
}

// 基础标签：默认常驻标签栏（首页不可关闭，其余可关闭隐藏）
const DEFAULT_TABS: NavTab[] = [
  { key: 'home', label: '首页', path: '/' },
  { key: 'ai-agent', label: 'AI Agent', path: '/ai-agent' },
  { key: 'skills', label: '技能管理', path: '/skills' },
  { key: 'system-monitor', label: '系统监控', path: '/system-monitor' },
]

// 标签栏状态：打开页面时通过 openTab 追加，关闭时移除
export const useTabsStore = defineStore('tabs', () => {
  // 动态标签列表，种子为基础标签
  const tabs = ref<NavTab[]>([...DEFAULT_TABS])

  // 打开页面时在标签栏注册标签（已存在则跳过，避免重复）
  function openTab(path: string, label?: string): void {
    if (tabs.value.some((t) => t.path === path)) return
    tabs.value.push({ key: path, label: label || path, path })
  }

  // 关闭标签（首页不可关闭）
  function closeTab(path: string): void {
    if (path === '/') return
    const idx = tabs.value.findIndex((t) => t.path === path)
    if (idx !== -1) tabs.value.splice(idx, 1)
  }

  function hasTab(path: string): boolean {
    return tabs.value.some((t) => t.path === path)
  }

  return { tabs, openTab, closeTab, hasTab }
})
