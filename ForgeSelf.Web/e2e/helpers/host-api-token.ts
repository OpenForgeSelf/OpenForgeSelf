import { execSync } from 'node:child_process'
import { pbkdf2Sync, createDecipheriv } from 'node:crypto'
import { readFileSync } from 'node:fs'

/**
 * 宿主真实 ApiToken 解析（供直连运行实例的 e2e 使用）。
 *
 * 后端 ApiToken 以 AES-256-CBC「v2:」密文存于 ForgeSetting.config，密钥来源优先级：
 *   配置 Encryption:Key > 环境变量 FORGESELF_ENCRYPTION_KEY > 机器派生密钥。
 * 本机 51888 实例走机器派生密钥（PBKDF2-HMAC-SHA256，熵 = "ForgeSelf|" + 注册表 MachineGuid，
 * 盐 = "ForgeSelf.SecretEncryption.v2.MachineBound"，迭代 210000），与 real-auth.ts 的
 * 「AIProvider 默认密钥」不同（那把只用于解 v1 旧密文）。故此处独立实现正确的密钥派生。
 *
 * 解析优先级：
 *   1. E2E_API_TOKEN 环境变量（globalSetup 首启明文 / 手动注入）→ 直接用；
 *   2. 否则读 FORGE_SETTING_CONFIG（默认 51888 本机实例数据根）解密 <ApiToken>。
 */
export function resolveHostApiToken(): string {
  if (process.env.E2E_API_TOKEN) return process.env.E2E_API_TOKEN

  const configPath =
    process.env.FORGE_SETTING_CONFIG ??
    process.env.E2E_FORGESETTING_CONFIG ??
    'C:/Users/12504/.forgeself/Config/ForgeSetting.config' // 51888 本机开发实例数据根

  const cfg = readFileSync(configPath, 'utf8')
  const match = cfg.match(/<ApiToken>([^<]+)<\/ApiToken>/)
  if (!match) throw new Error(`ForgeSetting.config 未找到 ApiToken：${configPath}`)

  let payload = match[1].trim()
  if (!payload.startsWith('v2:')) {
    throw new Error('ApiToken 非 v2 密文，需扩展解密逻辑（当前仅支持机器派生密钥解 v2）')
  }
  payload = payload.slice(3)

  const guid = process.env.FORGE_MACHINE_GUID ?? readMachineGuid()
  const key = pbkdf2Sync(
    `ForgeSelf|${guid}`,
    'ForgeSelf.SecretEncryption.v2.MachineBound',
    210_000,
    32,
    'sha256',
  )
  const data = Buffer.from(payload, 'base64')
  const iv = data.subarray(0, 16)
  const cipher = data.subarray(16)
  const decipher = createDecipheriv('aes-256-cbc', key, iv)
  return decipher.update(cipher) + decipher.final()
}

/** 读取 Windows 注册表 MachineGuid（机器派生密钥熵源）。非 Windows 请设 FORGE_MACHINE_GUID。 */
function readMachineGuid(): string {
  const out = execSync('reg query "HKLM\\SOFTWARE\\Microsoft\\Cryptography" /v MachineGuid', {
    encoding: 'utf8',
  })
  const m = out.match(/MachineGuid\s+REG_SZ\s+([0-9a-fA-F-]+)/)
  if (!m) throw new Error('无法从注册表读取 MachineGuid（非 Windows 请设 FORGE_MACHINE_GUID）')
  return m[1]
}
