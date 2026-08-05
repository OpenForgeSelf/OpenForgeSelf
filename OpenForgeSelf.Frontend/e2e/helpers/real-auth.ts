import { readFileSync } from 'node:fs'
import { createHash, createDecipheriv } from 'node:crypto'
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
  process.env.OPENFORGE_ENCRYPTION_KEY ?? 'OpenForgeSelf-AIProvider-Default-Encryption-Key'

let cachedKey: string | null = null

/** 从 ForgeSetting.config 解密当前真实 API 密钥（进程内缓存） */
export function getRealApiKey(): string {
  if (cachedKey) return cachedKey

  const xml = readFileSync(CONFIG_PATH, 'utf8')
  const match = xml.match(/<ApiToken>([^<]+)<\/ApiToken>/)
  if (!match) throw new Error('ForgeSetting.config 中未找到 ApiToken')

  const data = Buffer.from(match[1], 'base64')
  const key = createHash('sha256').update(KEY_SOURCE, 'utf8').digest()
  const decipher = createDecipheriv('aes-256-cbc', key, data.subarray(0, 16))
  const plain = Buffer.concat([decipher.update(data.subarray(16)), decipher.final()]).toString('utf8')

  if (!plain.startsWith('sk-')) {
    throw new Error('解密出的 API 密钥格式异常（应以 sk- 开头）')
  }
  cachedKey = plain
  return plain
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

