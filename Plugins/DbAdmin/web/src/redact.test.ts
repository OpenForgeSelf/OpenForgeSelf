import { describe, expect, it } from 'vitest'
import { MASK, hasPlaintextSecret, maskConnStr, maskPassword, redactText } from './redact'

// 下列值均为测试占位，不是任何真实密钥
const FAKE = 'unit-fake-pw'

describe('前端防御性脱敏', () => {
  it('连接串 password/pwd 掩码', () => {
    const s = maskConnStr(`Server=db;User Id=sa;Password=${FAKE};Pwd='${FAKE}'`)
    expect(s).not.toContain(FAKE)
    expect(s).toContain(`Password=${MASK}`)
    expect(s).toContain(`Pwd=${MASK}`)
  })
  it('Bearer 与 URI 凭据掩码', () => {
    expect(redactText(`Authorization: Bearer abc.def-123`)).toBe(`Authorization: Bearer ${MASK}`)
    expect(redactText(`postgres://u:${FAKE}@h:5432/d`)).toBe(`postgres://u:${MASK}@h:5432/d`)
  })
  it('已掩码的值保持原样（不重复改写格式）', () => {
    expect(redactText("UPDATE a SET api_key = '****' WHERE id = 1")).toBe("UPDATE a SET api_key = '****' WHERE id = 1")
  })
  it('无密钥文本原样', () => {
    expect(redactText('SELECT * FROM users WHERE id = 1')).toBe('SELECT * FROM users WHERE id = 1')
    expect(redactText(undefined)).toBe('')
  })
  it('密码框回显只有 **** 或空', () => {
    expect(maskPassword(FAKE)).toBe(MASK)
    expect(maskPassword('')).toBe('')
  })
  it('明文检测：掩码值不算明文', () => {
    expect(hasPlaintextSecret(`password=${MASK}`)).toBe(false)
    expect(hasPlaintextSecret(`password=${FAKE}`)).toBe(true)
    expect(hasPlaintextSecret(redactText(`password=${FAKE}`))).toBe(false)
  })
})
