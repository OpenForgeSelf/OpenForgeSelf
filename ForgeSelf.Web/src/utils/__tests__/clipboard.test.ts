import { describe, expect, it, vi, beforeEach } from 'vitest'
import { copyToClipboard } from '../clipboard'

/** execCommand 在 jsdom 不存在，统一 stub 环境 */
function installExecCommand(result: boolean): ReturnType<typeof vi.fn> {
  const exec = vi.fn().mockReturnValue(result)
  Object.defineProperty(document, 'execCommand', { value: exec, configurable: true, writable: true })
  return exec
}

describe('copyToClipboard', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('空文本直接返回 false，不触碰剪贴板', async () => {
    const write = vi.fn()
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: write }, configurable: true })
    await expect(copyToClipboard('')).resolves.toBe(false)
    expect(write).not.toHaveBeenCalled()
  })

  it('安全上下文走 navigator.clipboard.writeText', async () => {
    const write = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: write }, configurable: true })
    Object.defineProperty(window, 'isSecureContext', { value: true, configurable: true })
    await expect(copyToClipboard('gho-token')).resolves.toBe(true)
    expect(write).toHaveBeenCalledWith('gho-token')
  })

  it('clipboard.writeText 拒绝时回退 execCommand', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('denied')) },
      configurable: true,
    })
    Object.defineProperty(window, 'isSecureContext', { value: true, configurable: true })
    const exec = installExecCommand(true)
    await expect(copyToClipboard('text')).resolves.toBe(true)
    expect(exec).toHaveBeenCalledWith('copy')
  })

  it('非安全上下文直接走 execCommand 兜底', async () => {
    Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true })
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true })
    const exec = installExecCommand(true)
    await expect(copyToClipboard('text')).resolves.toBe(true)
    expect(exec).toHaveBeenCalledWith('copy')
  })

  it('两条路都失败返回 false 且不抛出', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('denied')) },
      configurable: true,
    })
    Object.defineProperty(window, 'isSecureContext', { value: true, configurable: true })
    installExecCommand(false)
    await expect(copyToClipboard('text')).resolves.toBe(false)
  })
})
