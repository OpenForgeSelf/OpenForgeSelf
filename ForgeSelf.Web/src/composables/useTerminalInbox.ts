import { onMounted, onUnmounted } from 'vue'
import { authFetch } from '@/services/authFetch'
import { useOpenPage } from '@/composables/useOpenPage'
import { terminalOpenQuery, type TerminalOpenItem } from '@/utils/terminalOpen'

/**
 * 主壳轮询智能终端收件箱。协议唤起或 Agent 建会话后，正在打开的宿主会切到智能终端并开标签。
 * 插件未启用（404）或尚未登录（401）时静默跳过。
 */
export function useTerminalInbox(): void {
  const { openPage } = useOpenPage()
  let timer: ReturnType<typeof setInterval> | undefined
  let stopped = false

  async function tick(): Promise<void> {
    try {
      const res = await authFetch('/api/smart-terminal/inbound')
      if (res.status === 404 || res.status === 401) return
      if (!res.ok) return
      const body = await res.json() as { items?: TerminalOpenItem[] }
      const items = body.items ?? []
      for (const item of items) {
        const search = new URLSearchParams(terminalOpenQuery(item)).toString()
        // 不把深链放进地址栏。路由只负责打开页面；参数走 sessionStorage + 事件，避免刷新重复开标签。
        sessionStorage.setItem('st-pending-open', search)
        openPage('/smart-terminal', '智能终端')
        window.dispatchEvent(new CustomEvent('forgeself-terminal-open', { detail: search }))
      }
    } catch {
      // 宿主还没就绪，或插件没有挂上
    }
  }

  onMounted(() => {
    void tick()
    timer = setInterval(() => {
      if (!stopped) void tick()
    }, 2000)
  })

  onUnmounted(() => {
    stopped = true
    if (timer) clearInterval(timer)
  })
}
