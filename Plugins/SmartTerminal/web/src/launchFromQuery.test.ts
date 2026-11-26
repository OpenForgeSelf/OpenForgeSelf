import { describe, expect, it } from 'vitest'
import { isLaunchQuery, parseLaunchQuery, scrubLaunchParamsFromUrl, stripConsumedLaunch, terminalFocusFromSearch, toCreateBody } from './launchFromQuery'

describe('parseLaunchQuery', () => {
  it('parses local profile + repeated step', () => {
    const q = 'st=1&profile=local&shell=bash&cwd=/tmp&step=echo%20hi&step=ls'
    const p = parseLaunchQuery(q)
    expect(p).not.toBeNull()
    expect(p!.request.profile).toBe('local')
    expect(p!.request.commands).toEqual(['echo hi', 'ls'])
    expect(p!.summary).toContain('profile=local')
    expect(p!.summary).toContain('steps=2')
    expect(p!.needsInteractivePassword).toBe(false)
  })

  it('parses run JSON steps', () => {
    const run = encodeURIComponent(JSON.stringify([
      { command: 'git', args: ['status'] },
      { command: 'pnpm', args: ['test'] },
    ]))
    const p = parseLaunchQuery(`st=1&profile=local&run=${run}`)
    expect(p!.request.commands?.[0]).toContain('git')
    expect(p!.request.commands?.[0]).toContain('status')
    expect(p!.request.steps?.length).toBe(2)
  })

  it('marks ssh without password as needsInteractivePassword', () => {
    const p = parseLaunchQuery('st=1&profile=ssh&host=10.0.0.1&user=root&step=uname')
    expect(p!.needsInteractivePassword).toBe(true)
    expect(p!.passwordInUrl).toBe(false)
    expect(p!.request.auth).toBe('interactive')
  })

  it('does not ask interactive password for auth=key', () => {
    const p = parseLaunchQuery('st=1&profile=ssh&host=h&user=u&auth=key&identityFile=~/.ssh/id_ed25519')
    expect(p!.needsInteractivePassword).toBe(false)
    expect(p!.request.auth).toBe('key')
  })

  it('rejects custom:<id> with readable error (no silent local)', () => {
    const p = parseLaunchQuery('st=1&profile=custom:build-box&step=echo')
    expect(p!.unsupportedProfileError).toMatch(/custom:build-box/)
    expect(p!.unsupportedProfileError).toMatch(/不会静默/)
    expect(p!.needsInteractivePassword).toBe(false)
  })

  it('parses env.NAME and csv env', () => {
    const p = parseLaunchQuery('st=1&profile=local&env.FOO=bar&env=BAZ:qux')
    expect(p!.request.env).toMatchObject({ FOO: 'bar', BAZ: 'qux' })
    expect(p!.summary).toContain('env=2')
  })

  it('keeps password only on the in-memory request', () => {
    const p = parseLaunchQuery('st=1&profile=ssh&host=h&user=u&password=SECRET')
    expect(p!.passwordInUrl).toBe(true)
    expect(p!.request.password).toBe('SECRET')
    expect(p!.summary).not.toContain('SECRET')
    expect(p!.summary).toContain('***REDACTED***')
    expect(p!.needsInteractivePassword).toBe(false)
    expect(p!.request.auth).toBe('interactive')
  })
})

describe('terminalFocusFromSearch', () => {
  it('带 sessionId 和 approvalId 时定位到那条待批', () => {
    expect(terminalFocusFromSearch('?sessionId=sess&approvalId=ap1')).toEqual({
      sessionId: 'sess',
      approvalId: 'ap1',
    })
  })

  it('没有会话时不打开', () => {
    expect(terminalFocusFromSearch('?approvalId=ap1')).toBeNull()
  })
})

describe('isLaunchQuery / scrub', () => {
  it('recognizes legacy type', () => {
    expect(isLaunchQuery(new URLSearchParams('type=ssh&host=h'))).toBe(true)
  })

  it('puts the url password on the session body for one POST', () => {
    const parsed = parseLaunchQuery('st=1&type=ssh&host=10.0.0.1&port=22&user=ops&step=uname&password=SECRET')
    const apply = toCreateBody(parsed!)
    expect('error' in apply).toBe(false)
    if ('error' in apply) return
    expect(apply.body.kind).toBe('ssh')
    expect(apply.body.host).toBe('10.0.0.1')
    expect(apply.body.user).toBe('ops')
    expect(apply.body.auth).toBe('interactive')
    expect(apply.body.password).toBe('SECRET')
    expect(apply.commands).toEqual(['uname'])
    expect(apply.warning).toBeUndefined()
    expect(parsed!.summary).not.toContain('SECRET')
  })

  it('refuses wsl-ssh instead of opening a local shell', () => {
    const parsed = parseLaunchQuery('st=1&profile=wsl-ssh&host=h&user=u')
    const apply = toCreateBody(parsed!)
    expect(apply).toEqual({ error: 'wsl-ssh 只保留协议位，这一版不打开会话。' })
  })

  it('strips a consumed launch so refresh does not open again', () => {
    const next = stripConsumedLaunch('http://local/smart-terminal?st=1&type=ssh&host=h&user=u#token=abc')
    expect(next).toBe('/smart-terminal#token=abc')
  })

  it('scrubs password and launch flags', () => {
    const href = 'http://local/smart-terminal?st=1&profile=ssh&password=x&host=h'
    // scrub uses history/location when available; in node it still returns cleaned path
    const next = scrubLaunchParamsFromUrl(href)
    expect(next).not.toContain('password=')
    expect(next).not.toMatch(/[?&]st=1/)
    expect(next).toContain('profile=ssh')
  })
})
