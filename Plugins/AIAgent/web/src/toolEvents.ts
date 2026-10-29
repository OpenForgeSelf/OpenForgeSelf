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

/** 工具事件的最小形状（后端 ToolCallsJson 与 SSE 事件都能投影到它）。 */
export interface ToolEventLike {
  name?: string
  args?: string
  arguments?: string
  result?: string
  success?: boolean
  pending?: boolean
  durationMs?: number
}

/** 与 agentBridge 的流式工具卡共用同一判据：写操作要打标。 */
const DANGER_RE =
  /(write|create|update|delete|remove|rename|move|terminal|command|exec|script|save|upload|kill|install|cleanup)/i

/**
 * 历史工具轨迹 → 组件的 tool 块。
 *
 * ★ 为什么必须与流式走同一个形状：流式期间工具卡由 agentBridge 现场发，
 *   刷新/切会话后由这里从 ToolCallsJson 重建。两处各写一份渲染，
 *   同一个工具调用就会在「当时看到的」和「回头看」的两个样子之间漂移 ——
 *   这正是删掉旧 ChatPanel 后暴露出来的缺口。
 */
export function toolEventsToBlocks(events: ToolEventLike[] | null | undefined) {
  const list = Array.isArray(events) ? events : []
  return list
    .filter((e) => e && (e.name || e.result))
    .map((e) => {
      const name = String(e.name || 'tool')
      return {
        type: 'tool' as const,
        name,
        /* pending 的保持 running：历史里仍有未结算的调用时，别假装它成功了 */
        status: e.pending ? ('running' as const) : e.success === false ? ('error' as const) : ('ok' as const),
        args: e.args ?? e.arguments,
        result: e.result,
        durationMs: e.durationMs,
        dangerous: DANGER_RE.test(name),
      }
    })
}
