import { describe, expect, it } from 'vitest'
import { isSensitiveEnvKey, redactAuditText, redactEnvForDisplay } from './secretDisplay'

describe('secretDisplay', () => {
  it('flags sensitive env keys', () => {
    expect(isSensitiveEnvKey('API_TOKEN')).toBe(true)
    expect(isSensitiveEnvKey('db_password')).toBe(true)
    expect(isSensitiveEnvKey('LANG')).toBe(false)
  })

  it('redacts env values for display', () => {
    const out = redactEnvForDisplay({
      LANG: 'C',
      API_TOKEN: 'super-secret-value-1234567890',
      NORMAL: 'ok',
    })!
    expect(out.LANG).toBe('C')
    expect(out.API_TOKEN).toBe('***')
    expect(out.NORMAL).toBe('ok')
  })

  it('redacts audit text env-like assignments', () => {
    const t = redactAuditText('launch env.SECRET_KEY=abc123 decision=allow')
    expect(t).toContain('SECRET_KEY=***')
    expect(t).not.toContain('abc123')
    expect(redactAuditText('SSHPASS=hunter2')).toContain('***REDACTED***')
  })
})
