/**
 * 复制编排（纯逻辑、依赖注入 ⇒ 可单测，形状同 AIAgent/web/src/sessionArchive.ts 的约定）：
 * 复制失败**不回显"已复制"了事**，而是转为"已选中文本，请按 Ctrl+C"，
 * 因为这个插件的全部价值就是把结果搬回聊天窗口，静默失败等于本轮报废。
 */

export type CopyKey = 'prompt' | 'result'

export interface CopyDeps {
  write: (text: string) => Promise<void>
  select: () => void
}

export type CopyOutcome = { kind: 'ok'; key: CopyKey } | { kind: 'fallback'; key: CopyKey }

export async function copyText(key: CopyKey, text: string, deps: CopyDeps): Promise<CopyOutcome> {
  if (!text) return { kind: 'fallback', key }
  try {
    await deps.write(text)
    return { kind: 'ok', key }
  } catch {
    deps.select()
    return { kind: 'fallback', key }
  }
}

/** 浏览器剪贴板写入是否可用（不可用时直接走选中回退，别等点击才发现）。 */
export function clipboardAvailable(): boolean {
  return typeof navigator !== 'undefined' && !!navigator.clipboard?.writeText
}
