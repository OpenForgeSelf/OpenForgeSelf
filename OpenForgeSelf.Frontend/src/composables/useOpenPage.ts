import { useRouter } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import { useTabsStore } from '@/stores/tabs'

// 统一的「打开页面」方法：导航的同时在标签栏注册标签。
// 全站所有打开页面的入口（侧边栏、功能列表、首页快捷入口等）都应走此方法，
// 以保证「导航」与「标签栏出现标签」行为一致。
export function useOpenPage() {
  const router = useRouter()
  const tabsStore = useTabsStore()

  // to 支持字符串路径或带 query 的路由位置；label 为标签栏显示文案（省略时回退路径）
  function openPage(to: RouteLocationRaw, label?: string): void {
    const path = typeof to === 'string' ? to : (to.path ?? '/')
    tabsStore.openTab(path, label)
    void router.push(to)
  }

  return { openPage }
}
