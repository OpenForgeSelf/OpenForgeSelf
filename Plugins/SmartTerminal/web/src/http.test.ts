import { describe, expect, it } from 'vitest'
import { ApiError } from './http'

describe('ApiError', () => {
  it('recognizes 409 password_required', () => {
    const err = new ApiError(409, '需要交互密码', { code: 'password_required', needsInteractivePassword: true }, '/api/smart-terminal/sessions')
    expect(err.status).toBe(409)
    expect(err.code).toBe('password_required')
    expect(err.needsInteractivePassword).toBe(true)
    expect(err.path).toBe('/api/smart-terminal/sessions')
    expect(err.message).toContain('交互密码')
  })
})
