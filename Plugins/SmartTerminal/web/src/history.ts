/** 把 GET /sessions/{id}/commands 的回包收成上/下键可用的命令行，避免 String(object) → [object Object]。 */
export function historyLine(entry: unknown): string | null {
  if (typeof entry === 'string') {
    const text = entry.trim()
    if (!text || text === '[object Object]') return null
    return text
  }
  if (!entry || typeof entry !== 'object') return null
  const o = entry as Record<string, unknown>
  for (const key of ['command', 'commandRedacted', 'Command', 'CommandRedacted', 'text', 'Text']) {
    const value = o[key]
    if (typeof value === 'string' && value.trim()) return value.trim()
  }
  return null
}

export function historyLines(payload: unknown): string[] {
  const list = Array.isArray(payload)
    ? payload
    : payload && typeof payload === 'object' && Array.isArray((payload as { items?: unknown[] }).items)
      ? (payload as { items: unknown[] }).items
      : []
  const out: string[] = []
  for (const item of list) {
    const line = historyLine(item)
    if (line) out.push(line)
  }
  return out
}
