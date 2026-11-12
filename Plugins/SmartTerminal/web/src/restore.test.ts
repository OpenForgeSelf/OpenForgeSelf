import { describe, expect, it } from 'vitest'
import { loadRestore, RESTORE_KEY, RESTORED_TOAST, saveRestore, toSnapshot } from './restore'
import type { Block, RestoreSnapshot } from './types'

const mem = (): Storage => {
  const map = new Map<string, string>()
  return {
    get length() { return map.size },
    clear: () => map.clear(),
    getItem: (k) => (map.has(k) ? map.get(k)! : null),
    key: (i) => [...map.keys()][i] ?? null,
    removeItem: (k) => { map.delete(k) },
    setItem: (k, v) => { map.set(k, v) },
  } as Storage
}

function snap(over: Partial<RestoreSnapshot> = {}): RestoreSnapshot {
  return {
    activeId: 's-1',
    railOpen: true,
    railTab: 'ai',
    tabs: [
      {
        id: 's-1',
        title: '本机开发',
        profileId: 'p-local-bash',
        kind: 'local',
        shell: 'bash',
        cwd: '~/work/forgeself',
        status: 'connected',
        host: null,
        user: null,
        auth: null,
        blocks: [{ id: 'b-1', seq: 1, kind: 'command', command: 'git status -sb', output: '## main', status: 'done', exitCode: 0 }],
      },
    ],
    ...over,
  }
}

describe('st:ui restore', () => {
  it('writes the locked key and never persists password or key material', () => {
    const storage = mem()
    const dirty = {
      ...snap(),
      tabs: [
        {
          ...snap().tabs[0],
          password: 'super-secret',
          privateKey: '-----BEGIN OPENSSH PRIVATE KEY-----',
          identityFile: '/home/ops/.ssh/id_ed25519',
          secretId: 'sec-1',
        } as any,
      ],
    }
    saveRestore(dirty, storage)
    expect(storage.getItem(RESTORE_KEY)).toBeTruthy()
    const raw = storage.getItem(RESTORE_KEY)!
    expect(raw).not.toMatch(/super-secret/)
    expect(raw).not.toMatch(/BEGIN OPENSSH/)
    expect(raw).not.toMatch(/password/)
    expect(raw).not.toMatch(/privateKey/)
    expect(raw).not.toMatch(/identityFile/)
    expect(raw).not.toMatch(/secretId/)
    const loaded = loadRestore(storage)!
    expect(loaded.tabs[0].title).toBe('本机开发')
    expect(loaded.tabs[0].auth).toBeNull()
    expect(RESTORED_TOAST).toContain('未恢复任何密码或密钥')
  })

  it('caps scrollback by lines then bytes', () => {
    const blocks: Block[] = Array.from({ length: 40 }, (_, i) => ({
      id: `b-${i}`,
      seq: i,
      kind: 'command',
      command: `echo ${i}`,
      output: 'x'.repeat(8000),
      status: 'done',
      exitCode: 0,
    }))
    const out = toSnapshot(snap({ tabs: [{ ...snap().tabs[0], blocks }] }))
    const kept = out.tabs[0].blocks
    expect(kept.length).toBeGreaterThan(0)
    expect(kept.length).toBeLessThan(40)
    expect(kept[0].id).not.toBe('b-0')
  })
})
