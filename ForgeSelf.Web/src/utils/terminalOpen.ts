/** 把收件箱的一条打开请求收成智能终端路由的 query。不含密码。 */

export interface TerminalOpenItem {
  id?: string
  query?: string | null
  sessionId?: string | null
  secretStripped?: boolean
}

export function terminalOpenQuery(item: TerminalOpenItem): Record<string, string> {
  const params = new URLSearchParams(item.query || '')
  for (const key of [...params.keys()]) {
    const lower = key.toLowerCase()
    if (
      lower === 'password'
      || lower === 'pwd'
      || lower === 'passwd'
      || lower === 'token'
      || lower === 'secret'
      || lower.includes('password')
      || lower.includes('sshpass')
    ) {
      params.delete(key)
    }
  }
  if (item.sessionId) params.set('sessionId', item.sessionId)
  params.set('st', '1')
  if (item.id) params.set('st_id', item.id)
  if (item.secretStripped) params.set('st_secret_stripped', '1')
  return Object.fromEntries(params.entries())
}
