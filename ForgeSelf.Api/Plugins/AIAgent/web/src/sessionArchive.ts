/**
 * 会话归档的「确认 → 执行」编排。
 *
 * 起因（用户反馈 2026-09-22）：归档按钮点了就直接归档，没有二次确认。
 * 归档虽是软标记（不删消息、可取消），但会话会立刻从 agent 页列表消失，属于
 * 「可见状态突变」的操作，必须先问一句再执行。
 *
 * 设计要点：
 * - 本模块**不依赖任何 UI 框架**：确认动作由调用方注入（生产传 ElMessageBox 版，
 *   见 AiAgentView.onArchiveSession；单测传假实现）。这样「用户取消时一个请求都不发」
 *   这条关键语义可以被 jsdom 单测直接锁住，无需在测试里弹真弹窗。
 * - 归档 = 软标记，不删任何消息（与 deleteSession 的硬删严格区分）。
 */
import { archiveSession } from './http'

/** 确认回调：返回 true 表示用户确认归档，false 表示取消。 */
export type ArchiveConfirm = (sessionId: string, title?: string) => Promise<boolean>

/** 归档编排结果：已归档 / 用户取消 / 执行失败。 */
export type ArchiveOutcome = 'archived' | 'cancelled' | 'failed'

/** 归档编排结果详情（failed 时带错误文案）。 */
export interface ArchiveResult {
  /** 本次编排的最终结果。 */
  outcome: ArchiveOutcome
  /** 仅 outcome === 'failed' 时存在，供界面提示。 */
  error?: string
}

/**
 * 确认后归档。
 *
 * 顺序固定为「先确认、后请求」：用户取消 → 直接返回 cancelled，**不发起任何网络请求**
 * （回归守卫：绝不允许确认被跳过而直接归档）。
 *
 * @param sessionId 会话全 id（原样透传给后端，做前缀剥离会归档到错会话）
 * @param confirm 确认动作（注入以便单测）
 * @param title 会话标题（仅用于确认文案，缺失时由 confirm 自行回退到 id）
 */
export async function archiveSessionWithConfirm(
  sessionId: string,
  confirm: ArchiveConfirm,
  title?: string,
): Promise<ArchiveResult> {
  const confirmed = await confirm(sessionId, title)
  if (!confirmed) return { outcome: 'cancelled' }

  try {
    await archiveSession(sessionId, true)
    return { outcome: 'archived' }
  } catch (e) {
    return { outcome: 'failed', error: e instanceof Error ? e.message : String(e) }
  }
}
