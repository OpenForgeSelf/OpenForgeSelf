import type { ToolEvent } from './types'

/**
 * B9-2：把一条 tool_result 结算到对应的 pending 工具事件上（**FIFO 前溯**）。
 *
 * B8 批次起后端帧序为「全部 tool/call 先落 → 批量执行 → 逐 tool/result」（model-ordered commit），
 * 同名工具 2+ 次调用时结果按模型顺序依次到达 —— 必须取**最早**的同名 pending 事件配对；
 * 旧的 LIFO 后溯（从后往前找同名 pending）会把同名多次调用的结果互换展示（QA 终验外送缺陷）。
 */
export function settleToolEventFifo(
  events: ToolEvent[],
  e: { name?: string; result?: string; success?: boolean },
): void {
  for (let i = 0; i < events.length; i++) {
    const ev = events[i]
    if (ev && ev.pending && ev.name === e.name) {
      ev.result = e.result
      ev.success = e.success
      ev.pending = false
      return
    }
  }
  // 没找到（异常情况：result 先于 call 到达等）则补一条
  events.push({ name: e.name, result: e.result, success: e.success, pending: false })
}
