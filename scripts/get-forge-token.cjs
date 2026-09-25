#!/usr/bin/env node
/**
 * get-forge-token —— 一键获取当前宿主（ForgeSetting.config）的有效明文 API token。
 *
 * 用法: node scripts/get-forge-token.cjs [--config <path>]
 *   - 默认读取 %USERPROFILE%\.forgeself\Config\ForgeSetting.config（运行态 publish 宿主真源）
 *   - 覆盖: --config <path> 或环境变量 FORGE_SETTING_CONFIG
 *   - MachineGuid 覆盖: FORGE_MACHINE_GUID（非 Windows / 无法读注册表时）
 * 输出: 明文 token（sk-...）到 stdout。
 *
 * 用途（走查标准步骤的一部分，禁止每次现写解密探针）：
 *   - 运行态宿主（如 :51888）bu/Playwright 手工走查通道：拿 token 注入
 *     localStorage['forge_api_token'] 后导航。
 *   - e2e 全自动场景请直接走 e2e globalSetup（e2e/helpers/real-auth.ts，自动起宿主+注入）。
 *
 * 注意: 宿主每次启动都会轮换 ApiToken —— 跑本脚本前确认目标宿主为当前运行实例，
 *       否则解密出的仍是旧 token（宿主要再轮换一次才失效，实测 401）。
 * 算法与 AesSecretEncryptionService / real-auth.ts 一致（v2 = PBKDF2-HMAC-SHA256 210k +
 * AES-256-CBC，密钥熵 = `ForgeSelf|<MachineGuid>`，盐 = `ForgeSelf.SecretEncryption.v2.MachineBound`）。
 */
const fs = require('node:fs')
const os = require('node:os')
const path = require('node:path')
const crypto = require('node:crypto')
const { execSync } = require('node:child_process')

const args = process.argv.slice(2)
const configPath = (() => {
  const i = args.indexOf('--config')
  if (i >= 0 && args[i + 1]) return args[i + 1]
  if (process.env.FORGE_SETTING_CONFIG) return process.env.FORGE_SETTING_CONFIG
  return path.join(os.homedir(), '.forgeself', 'Config', 'ForgeSetting.config')
})()

function readMachineGuid() {
  if (process.env.FORGE_MACHINE_GUID) return process.env.FORGE_MACHINE_GUID
  const out = execSync(
    'reg query "HKLM\\SOFTWARE\\Microsoft\\Cryptography" /v MachineGuid',
    { encoding: 'utf8' },
  )
  const m = out.match(/MachineGuid\s+REG_SZ\s+([0-9a-fA-F-]+)/)
  if (!m) throw new Error('无法从注册表读取 MachineGuid（非 Windows 请设 FORGE_MACHINE_GUID）')
  return m[1]
}

function decryptApiToken(payload) {
  if (payload.startsWith('v2:')) {
    const body = payload.slice(3)
    const key = crypto.pbkdf2Sync(
      `ForgeSelf|${readMachineGuid()}`,
      'ForgeSelf.SecretEncryption.v2.MachineBound',
      210_000,
      32,
      'sha256',
    )
    const data = Buffer.from(body, 'base64')
    const iv = data.subarray(0, 16)
    const decipher = crypto.createDecipheriv('aes-256-cbc', key, iv)
    return Buffer.concat([decipher.update(data.subarray(16)), decipher.final()]).toString('utf8')
  }
  // v1 回退（AIProvider 默认密钥）
  const key = crypto
    .createHash('sha256')
    .update('ForgeSelf-AIProvider-Default-Encryption-Key', 'utf8')
    .digest()
  const data = Buffer.from(payload, 'base64')
  const decipher = crypto.createDecipheriv('aes-256-cbc', key, data.subarray(0, 16))
  return Buffer.concat([decipher.update(data.subarray(16)), decipher.final()]).toString('utf8')
}

const xml = fs.readFileSync(configPath, 'utf8')
const match = xml.match(/<ApiToken>([^<]+)<\/ApiToken>/)
if (!match) throw new Error(`ForgeSetting.config 未找到 ApiToken: ${configPath}`)
const token = decryptApiToken(match[1].trim())
process.stdout.write(`${token}\n`)
