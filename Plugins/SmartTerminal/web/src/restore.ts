import type { Block, RestoreSnapshot, RestoreTab } from './types'

export const RESTORE_KEY = 'st:ui'
export const MAX_RESTORE_LINES = 1000
export const MAX_RESTORE_BYTES = 100 * 1024

const SECRET_KEYS = /password|passwd|secret|privatekey|private_key|identity(?:file)?|passphrase|sshpass|credential|token|keymaterial/i

export function capBlocks(blocks: Block[]): Block[] {
  const kept: Block[] = []
  let lines = 0
  let bytes = 0
  for (let i = blocks.length - 1; i >= 0; i--) {
    const b = blocks[i]
    const text = `${b.command}\n${b.output ?? ''}`
    const addLines = text.split('\n').length
    const addBytes = new TextEncoder().encode(text).length
    if (kept.length && (lines + addLines > MAX_RESTORE_LINES || bytes + addBytes > MAX_RESTORE_BYTES)) break
    kept.push(b)
    lines += addLines
    bytes += addBytes
  }
  return kept.reverse()
}

function stripSecrets<T>(value: T): T {
  if (value == null) return value
  if (Array.isArray(value)) return value.map((v) => stripSecrets(v)) as T
  if (typeof value === 'object') {
    const out: Record<string, unknown> = {}
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      if (SECRET_KEYS.test(k)) continue
      out[k] = stripSecrets(v)
    }
    return out as T
  }
  return value
}

export function toSnapshot(input: RestoreSnapshot): RestoreSnapshot {
  const tabs: RestoreTab[] = input.tabs.map((t) =>
    stripSecrets({
      id: t.id,
      title: t.title,
      profileId: t.profileId ?? null,
      kind: t.kind,
      shell: t.shell ?? null,
      cwd: t.cwd ?? null,
      status: t.status,
      host: t.host ?? null,
      port: t.port ?? null,
      user: t.user ?? null,
      auth: t.auth ?? null,
      blocks: capBlocks(t.blocks ?? []),
    }),
  )
  return stripSecrets({
    activeId: input.activeId,
    railOpen: !!input.railOpen,
    railTab: input.railTab === 'snippets' || input.railTab === 'history' ? input.railTab : 'ai',
    tabs,
  })
}

export function saveRestore(snapshot: RestoreSnapshot, storage: Storage = sessionStorage): void {
  try {
    storage.setItem(RESTORE_KEY, JSON.stringify(toSnapshot(snapshot)))
  } catch {
    // quota or private mode — ignore
  }
}

export function loadRestore(storage: Storage = sessionStorage): RestoreSnapshot | null {
  try {
    const raw = storage.getItem(RESTORE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as RestoreSnapshot
    if (!parsed || !Array.isArray(parsed.tabs)) return null
    return toSnapshot(parsed)
  } catch {
    return null
  }
}

export const RESTORED_TOAST = '已恢复标签与近期输出，未恢复任何密码或密钥'
