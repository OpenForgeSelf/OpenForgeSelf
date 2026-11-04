import { describe, expect, it } from 'vitest'
import { ApiError } from './http'

describe('ApiError', () => {
  it('carries status and data for needsInteractivePassword', () => {
    const err = new ApiError(400, '需要交互密码', { needsInteractivePassword: true })
    expect(err.status).toBe(400)
    expect((err.data as any).needsInteractivePassword).toBe(true)
    expect(err.message).toContain('交互密码')
  })
})
