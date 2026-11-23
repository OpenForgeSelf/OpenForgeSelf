/**
 * 全站统一剪贴板写入（2026-11-22 输入20 盘点后固化的复用件）。
 * - 优先 navigator.clipboard.writeText（安全上下文）
 * - 失败回退 document.execCommand('copy')（http://localhost 之外的非安全上下文兜底）
 * - 全失败抛出，调用方负责用户提示；成功返回 true
 */

export async function copyToClipboard(text: string): Promise<boolean> {
  if (!text) return false
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // 落到 execCommand 兜底
  }
  try {
    const textarea = document.createElement('textarea')
    textarea.value = text
    textarea.style.position = 'fixed'
    textarea.style.opacity = '0'
    document.body.appendChild(textarea)
    textarea.select()
    const ok = document.execCommand('copy')
    document.body.removeChild(textarea)
    return ok
  } catch {
    return false
  }
}
