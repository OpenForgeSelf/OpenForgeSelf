import { describe, expect, it } from 'vitest'
import { terminalOpenQuery } from './terminalOpen'

describe('terminalOpenQuery', () => {
  it('keeps the ssh target and drops a password', () => {
    const query = terminalOpenQuery({
      id: 'abc',
      query: 'type=ssh&host=10.0.0.1&user=ops&password=SECRET',
      secretStripped: true,
    })
    expect(query.type).toBe('ssh')
    expect(query.host).toBe('10.0.0.1')
    expect(query.user).toBe('ops')
    expect(query.st).toBe('1')
    expect(query.st_id).toBe('abc')
    expect(query.st_secret_stripped).toBe('1')
    expect(JSON.stringify(query)).not.toContain('SECRET')
    expect(query.password).toBeUndefined()
  })

  it('attaches an agent session instead of inventing a host', () => {
    const query = terminalOpenQuery({ id: 'n1', sessionId: 's-9' })
    expect(query.sessionId).toBe('s-9')
    expect(query.st).toBe('1')
    expect(query.st_id).toBe('n1')
  })
})
