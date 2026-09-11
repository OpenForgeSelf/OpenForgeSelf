import { readFileSync } from 'node:fs'
import { createHash, createDecipheriv, pbkdf2Sync } from 'node:crypto'
import { execSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { Page } from '@playwright/test'

/**
 * 真实后端认证辅助 —— 供所有 E2E 测试共享。
 *
 * 后端 API 密钥密文存储在发布版 ForgeSetting.config 的 ApiToken 字段（AES-256-CBC，
 * IV 前缀 16 字节，密钥源见 AesSecretEncryptionService.ResolveKey）。本模块在运行时
 * 解密出当前真实明文密钥，注入浏览器 localStorage（forge_api_token），使前端请求
 * 携带真实 Authorization 头，实现"真实认证"。
 *
 * 与旧硬编码 REAL_API_KEY 的区别：密钥轮换（regenerate）后旧值立即失效，
 * 硬编码会导致 status 401；本模块每次从配置解密，始终拿到当前有效密钥。
 */

/** 后端配置文件路径（发布版），可通过 FORGE_SETTING_CONFIG 覆盖 */
const CONFIG_PATH =
  process.env.FORGE_SETTING_CONFIG ??
  path.resolve(fileURLToPath(new URL('../../../publish/Config/ForgeSetting.config', import.meta.url)))

/** 与 AesSecretEncryptionService.ResolveKey 一致的密钥源（配置/环境变量/默认） */
const KEY_SOURCE =
  process.env.FORGESELF_ENCRYPTION_KEY ?? 'ForgeSelf-AIProvider-Default-Encryption-Key'

let cachedKey: string | null = null

/** 从 ForgeSetting.config 解密当前真实 API 密钥（进程内缓存） */
export function getRealApiKey(): string {
  // e2e globalSetup 首启拿到的明文 token 优先（绕开文件落盘/解密，最稳）
  if (process.env.E2E_API_TOKEN) return process.env.E2E_API_TOKEN
  if (cachedKey) return cachedKey

  const xml = readFileSync(CONFIG_PATH, 'utf8')
  const match = xml.match(/<ApiToken>([^<]+)<\/ApiToken>/)
  if (!match) throw new Error('ForgeSetting.config 中未找到 ApiToken')

  const plain = decryptApiToken(match[1].trim())
  cachedKey = plain
  return plain
}

/**
 * 解密 ForgeSetting.config 中的 ApiToken 密文。
 * - **v2（机器派生密钥，PBKDF2-HMAC-SHA256 210k）**：030 认证升级后的系统标准格式，
 *   密钥熵 = `ForgeSelf|<MachineGuid>`，盐 = `ForgeSelf.SecretEncryption.v2.MachineBound`。
 *   与 `e2e/helpers/host-api-token.ts` 同算法（后者因本模块旧 v1 写法解不了 v2 而另建，现合并）。
 * - **v1（AIProvider 默认密钥 sha256）**：历史格式，向后兼容。
 * 优先按 v2 解密；非 `v2:` 前缀则回退 v1。
 */
function decryptApiToken(payload: string): string {
  if (payload.startsWith('v2:')) {
    const body = payload.slice(3)
    const guid = process.env.FORGE_MACHINE_GUID ?? readMachineGuid()
    const key = pbkdf2Sync(
      `ForgeSelf|${guid}`,
      'ForgeSelf.SecretEncryption.v2.MachineBound',
      210_000,
      32,
      'sha256',
    )
    const data = Buffer.from(body, 'base64')
    const iv = data.subarray(0, 16)
    const cipher = data.subarray(16)
    const decipher = createDecipheriv('aes-256-cbc', key, iv)
    return decipher.update(cipher) + decipher.final()
  }

  // v1 回退（AIProvider 默认密钥）
  const data = Buffer.from(payload, 'base64')
  const key = createHash('sha256').update(KEY_SOURCE, 'utf8').digest()
  const decipher = createDecipheriv('aes-256-cbc', key, data.subarray(0, 16))
  const plain = Buffer.concat([decipher.update(data.subarray(16)), decipher.final()]).toString('utf8')
  if (!plain.startsWith('sk-')) {
    throw new Error('解密出的 API 密钥格式异常（应以 sk- 开头）')
  }
  return plain
}

/** 读取 Windows 注册表 MachineGuid（v2 机器派生密钥熵源）。非 Windows 请设 FORGE_MACHINE_GUID。 */
function readMachineGuid(): string {
  const out = execSync('reg query "HKLM\\SOFTWARE\\Microsoft\\Cryptography" /v MachineGuid', {
    encoding: 'utf8',
  })
  const m = out.match(/MachineGuid\s+REG_SZ\s+([0-9a-fA-F-]+)/)
  if (!m) throw new Error('无法从注册表读取 MachineGuid（非 Windows 请设 FORGE_MACHINE_GUID）')
  return m[1]
}

/** 在页面加载前注入真实 API 密钥到 localStorage，使请求携带真实认证 */
export async function injectRealApiKey(page: Page): Promise<void> {
  const key = getRealApiKey()
  await page.addInitScript((token) => {
    localStorage.setItem('forge_api_token', token)
  }, key)
}

/**
 * 清除解密缓存（密钥轮换后调用）。
 * 真实 regenerate 会更新 ForgeSetting.config 的 ApiToken，缓存需失效
 * 以便后续 getRealApiKey() 重新从配置文件解密新密钥。
 */
export function clearRealApiKeyCache(): void {
  cachedKey = null
}

