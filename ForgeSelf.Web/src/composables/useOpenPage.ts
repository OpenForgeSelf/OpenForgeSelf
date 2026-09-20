import { useRouter } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import { useTabsStore } from '@/stores/tabs'
import { useUsageStatsStore } from '@/stores/usageStats'

// 统一的「打开页面」方法：导航的同时在标签栏注册标签，并记录一次功能使用。
// 全站所有打开页面的入口（侧边栏、功能列表、首页快捷入口等）都应走此方法，
// 以保证「导航」「标签栏出现标签」「使用度统计」三处行为一致。
export function useOpenPage() {
  const router = useRouter()
  const tabsStore = useTabsStore()
  const usageStore = useUsageStatsStore()

  // to 支持字符串路径或带 query 的路由位置；label 为标签栏显示文案（省略时回退路径）
  function openPage(to: RouteLocationRaw, label?: string): void {
    const path = typeof to === 'string' ? to : (to.path ?? '/')
    tabsStore.openTab(path, label)
    // 使用度统计走 path 维度：首页入口排序、后续的功能画像共用这一份数据
    usageStore.recordVisit(path)
    void router.push(to)
  }

  return { openPage }
}
