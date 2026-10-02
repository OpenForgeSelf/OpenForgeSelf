/**
 * 序号守卫（v3 新增，M2 竞态纪律的统一小工具）。
 *
 * 用法：发起异步时 `const run = createLatest()`；响应/回调回来时 `if (!run.isCurrent()) return`。
 * 凡是"await 后写共享 ref"的地方都用它：向导推荐、展厅缩略图、微调防抖 ——
 * 旧响应后到不得覆盖新状态（03-plan §C / 自查表 #19 的历史教训）。
 */
let seq = 0

export interface Latest {
  /** 本序号是否仍是全局最新一次（被后续 `createLatest()` 取代则为 false） */
  isCurrent(): boolean
}

/** 领取一个单调递增的序号；模块级计数器，任何实例都能互相作废 */
export function createLatest(): Latest {
  const id = ++seq
  return { isCurrent: () => id === seq }
}
