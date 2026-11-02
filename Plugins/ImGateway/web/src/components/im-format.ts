/**
 * IM 网关会话视图的格式化工具（PILOT-078）。
 * 纯函数，供 ChatList / ChatThread 复用；不放 http.ts（那是网络层）。
 */

/** 相对时间：刚刚 / N 分钟前 / N 小时前 / 昨天 / M月D日。 */
export function relativeTime(iso: string): string {
  if (!iso) return ''
  const t = new Date(iso).getTime()
  if (Number.isNaN(t)) return ''
  const diff = Date.now() - t
  const minute = 60_000
  const hour = 3_600_000
  const day = 86_400_000
  if (diff < minute) return '刚刚'
  if (diff < hour) return `${Math.floor(diff / minute)} 分钟前`
  if (diff < day) return `${Math.floor(diff / hour)} 小时前`
  if (diff < day * 2) return '昨天'
  const d = new Date(t)
  return `${d.getMonth() + 1}月${d.getDate()}日`
}

/** 气泡时间：当天只显示 HH:mm，更早显示 M-D HH:mm。 */
export function bubbleTime(iso: string): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const hm = `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
  const now = new Date()
  const sameDay =
    d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth() && d.getDate() === now.getDate()
  return sameDay ? hm : `${d.getMonth() + 1}-${d.getDate()} ${hm}`
}

/** 头像字符：显示名首字符（无名称回落「?」）。 */
export function avatarText(name: string): string {
  return (name || '?').charAt(0).toUpperCase()
}
