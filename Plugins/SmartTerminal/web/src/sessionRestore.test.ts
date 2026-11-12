import { afterEach, describe, expect, it } from 'vitest'
import {
  SESSION_RESTORE_KEY,
  buildSnapshot,
  clearSnapshot,
  loadSnapshot,
  removeTabFromSnapshot,
  saveSnapshot,
  trimChunks,
} from './sessionRestore'
import type { OutputChunk, TerminalSession } from './types'

class MemoryStorage {
  store = new Map<string, string>()
  getItem(k: string) { return this.store.has(k) ? this.store.get(k)! : null }
  setItem(k: string, v: string) { this.store.set(k, String(v)) }
  removeItem(k: string) { this.store.delete(k) }
}

const mem = new MemoryStorage()

afterEach(() => {
  mem.store.clear()
  clearSnapshot()
})

Object.defineProperty(globalThis, 'sessionStorage', {
  value: mem,
  configurable: true,
})

function chunk(i: number, text: string): OutputChunk {
  return { index: i, stream: 'stdout', text, at: new Date().toISOString() }
}

const sess = (id: string, title: string): TerminalSession => ({
  sessionId: id,
  title,
  shellId: 'bash',
  shellDisplayName: 'Bash',
  cwd: '/tmp',
  permissionMode: 'AskWrite',
  status: 'idle',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  commandCount: 0,
  launchType: 'ssh',
  remoteHost: '10.0.0.1',
  remoteUser: 'root',
})

describe('trimChunks', () => {
  it('keeps at most 1000 lines', () => {
    const many = Array.from({ length: 1200 }, (_, i) => chunk(i, `line-${i}`))
    const t = trimChunks(many)
    expect(t.length).toBe(1000)
    expect(t[0].text).toBe('line-200')
  })

  it('caps by ~100KB', () => {
    const big = 'x'.repeat(10_000)
    const many = Array.from({ length: 20 }, (_, i) => chunk(i, big))
    const t = trimChunks(many)
    const bytes = t.reduce((n, c) => n + new TextEncoder().encode(c.text).length, 0)
    expect(bytes).toBeLessThanOrEqual(100 * 1024 + 10_000) // last included then cut
    expect(t.length).toBeLessThan(20)
  })
})

describe('snapshot roundtrip', () => {
  it('saves and loads without password fields', () => {
    const snap = buildSnapshot({
      activeId: 'a',
      sessions: [sess('a', 'A'), sess('b', 'B')],
      connectionsBySession: {
        a: { profile: 'ssh', host: '10.0.0.1', user: 'root', authMethod: 'interactive' },
      },
      chunksBySession: { a: [chunk(0, 'hello')], b: [] },
      commandsBySession: {},
      nextIndexBySession: { a: 1 },
    })
    // inject password-like junk to ensure strip
    ;(snap as any).password = 'NOPE'
    ;(snap.tabs[0] as any).password = 'NOPE'
    saveSnapshot(snap)
    const raw = mem.getItem(SESSION_RESTORE_KEY)!
    expect(raw).not.toContain('NOPE')
    expect(raw).not.toMatch(/"password"\s*:/)
    const loaded = loadSnapshot()!
    expect(loaded.tabs.map(t => t.title)).toEqual(['A', 'B'])
    expect(loaded.activeId).toBe('a')
    expect(loaded.chunksBySession.a[0].text).toBe('hello')
    expect(loaded.tabs[0].connection?.host).toBe('10.0.0.1')
  })

  it('removeTab burns that tab buffer', () => {
    saveSnapshot(buildSnapshot({
      activeId: 'a',
      sessions: [sess('a', 'A'), sess('b', 'B')],
      chunksBySession: { a: [chunk(0, 'a')], b: [chunk(0, 'b')] },
      commandsBySession: {},
      nextIndexBySession: {},
    }))
    removeTabFromSnapshot('a')
    const loaded = loadSnapshot()!
    expect(loaded.tabs.map(t => t.sessionId)).toEqual(['b'])
    expect(loaded.chunksBySession.a).toBeUndefined()
    expect(loaded.activeId).toBe('b')
  })
})
