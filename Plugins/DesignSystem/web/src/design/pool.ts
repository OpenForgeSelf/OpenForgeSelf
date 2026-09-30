/**
 * 限流并发：把一批异步任务按固定上限跑完。
 *
 * 为什么界面需要它：导出页一次要读十几份格式预览加十类实体行数，全并发 = 二十几个请求同时打到
 * 宿主那个 SQLite 库上，实测会把其中随机一个顶成 500（`database is locked`）。
 * 只读请求本来就带退避重试，所以界面上多半看不出来 —— 但重试有上限，而且每次重试都在拖慢这一页。
 * 限流只解决"界面自己造出来的争抢"，后端的跨请求写锁是另一件事（不在这里掩盖）。
 */
export async function mapLimit<T>(items: readonly T[], limit: number, run: (item: T) => Promise<void>): Promise<void> {
  if (!items.length || limit < 1) return
  let cursor = 0
  const workers = Array.from({ length: Math.min(limit, items.length) }, async () => {
    while (cursor < items.length) {
      const index = cursor++
      await run(items[index])
    }
  })
  await Promise.all(workers)
}
