/**
 * Runtime 库存「一键探测」纯逻辑（与面板「刷新」分离）。
 * UI 层只负责调 probe API / 写卡状态；并发与聚合文案在此可单测。
 */

export const PROBE_ALL_CONCURRENCY = 3

export type ProbeAllSummary = { ok: number; err: number; total: number }

/**
 * 有界并发池：最多 `concurrency` 个 worker 并行消费 `items`。
 * 用于一键探测；禁止无上限 Promise.all 打爆本机 CLI。
 */
export async function mapPool<T, R>(
  items: readonly T[],
  concurrency: number,
  worker: (item: T, index: number) => Promise<R>,
): Promise<R[]> {
  const n = items.length
  if (n === 0) return []
  const limit = Math.max(1, Math.min(concurrency, n))
  const results: R[] = new Array(n)
  let cursor = 0

  async function run(): Promise<void> {
    while (cursor < n) {
      const i = cursor++
      results[i] = await worker(items[i] as T, i)
    }
  }

  await Promise.all(Array.from({ length: limit }, () => run()))
  return results
}

/** 聚合提示文案（与 AgentHubView 横幅一致）。 */
export function formatProbeAllMessage(s: ProbeAllSummary): { text: string; level: 'ok' | 'err' } {
  const { ok, err, total } = s
  if (err === 0) return { text: `一键探测完成：${ok}/${total} 全部成功`, level: 'ok' }
  if (ok === 0) return { text: `一键探测完成：${err}/${total} 全部失败（见各卡错误）`, level: 'err' }
  return { text: `一键探测完成：${ok} 成功 · ${err} 失败（共 ${total}）`, level: 'err' }
}
