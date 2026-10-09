import { execSync } from 'node:child_process'
import { readFileSync, unlinkSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { FullConfig } from '@playwright/test'
import { releasePort } from './helpers/free-port'

/**
 * 统一 e2e 共享基础设施（globalTeardown）
 * ----------------------------------------------------------------
 * 只杀本轮回合拉起的宿主进程（按 PID 杀进程树），不误伤用户手动实例。
 * 收尾同时：释放端口认领（free-port 注册表）+ 清 current.json（仅当仍指向本轮回合）。
 */
export default async function globalTeardown(
  _config: FullConfig,
  payload: { e2eRoot?: string; hostPid?: number },
) {
  const pid = payload?.hostPid
  if (pid) {
    try {
      if (process.platform === 'win32') {
        // /t 杀进程树（含 dotnet 子进程），/f 强制；execSync 同步等待，避免 Playwright 收尾截断杀进程
        execSync(`taskkill /pid ${pid} /t /f`, { stdio: 'ignore' })
      } else {
        process.kill(pid, 'SIGTERM')
      }
    } catch {
      /* 忽略：进程可能已退出 */
    }
  }

  // 释放本轮回合认领的前后端端口（vite 尚未停，但端口仍被绑定，他 worktree 探测会跳过）
  for (const p of [process.env.E2E_FRONTEND_PORT, process.env.E2E_BACKEND_PORT]) {
    if (p) releasePort(Number(p))
  }

  // 清 current.json：仅当它仍指向本轮回合（防止误删随后新回合写入的快照）
  try {
    const currentJson = path.resolve(
      path.dirname(fileURLToPath(import.meta.url)),
      '..',
      '..',
      '.temp',
      'e2e',
      'current.json',
    )
    const cur = JSON.parse(readFileSync(currentJson, 'utf8')) as { e2eRoot?: string }
    if (cur?.e2eRoot && cur.e2eRoot === payload?.e2eRoot) unlinkSync(currentJson)
  } catch {
    /* 无 current.json / 已被清理 */
  }
}
