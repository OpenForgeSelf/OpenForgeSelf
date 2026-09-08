import { spawn } from 'node:child_process'
import type { FullConfig } from '@playwright/test'

/**
 * 统一 e2e 共享基础设施（globalTeardown）
 * ----------------------------------------------------------------
 * 只杀本轮回合拉起的宿主进程（按 PID 杀进程树），不误伤用户手动实例。
 */
export default async function globalTeardown(
  _config: FullConfig,
  payload: { e2eRoot?: string; hostPid?: number },
) {
  const pid = payload?.hostPid
  if (!pid) return
  try {
    if (process.platform === 'win32') {
      // /t 杀进程树（含 dotnet 子进程），/f 强制
      spawn('taskkill', ['/pid', String(pid), '/t', '/f'], { stdio: 'ignore' })
    } else {
      process.kill(pid, 'SIGTERM')
    }
  } catch {
    /* 忽略：进程可能已退出 */
  }
}
