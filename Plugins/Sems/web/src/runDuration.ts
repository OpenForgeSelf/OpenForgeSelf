/** 解析后端 startedAt（Launched 为本地时间 ISO；Detected 可能为空）。 */
export function parseStartedAt(raw?: string): Date | null {
  if (!raw) return null
  const d = new Date(raw)
  if (Number.isNaN(d.getTime()) || d.getTime() <= 0) return null
  return d
}

/** 相对「已运行」文案；nowMs 由外部每秒 tick 传入以保证界面刷新。 */
export function formatRunDuration(startedAt: string | undefined, nowMs: number): string {
  const start = parseStartedAt(startedAt)
  if (!start) return '—'
  const ms = nowMs - start.getTime()
  if (ms < 0) return '0s'
  const s = Math.floor(ms / 1000)
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  if (h > 0) return `${h}h${m}m`
  if (m > 0) return `${m}m${sec}s`
  return `${sec}s`
}
