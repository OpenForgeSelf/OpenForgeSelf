/**
 * e2e 环境统一读取（PILOT-050 T2）
 * ----------------------------------------------------------------
 * 后端/前端地址的唯一真源链：环境变量 → `.temp/e2e/current.json`（校验宿主 PID 存活）
 * → 默认值（7102/7002，向后兼容单 worktree 既有行为）。
 *
 * 替代旧做法：各 spec 自行扫描 `.temp/e2e/` 下的时间戳目录（目录稳定化后必然失配，
 * 见 real-auth.ts 旧正则）。所有 spec 一律 import 本模块取地址，禁止再硬编码端口。
 */
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

/** e2e 运行态根目录：`<仓库根>/.temp/e2e/`（与 global-setup 的 REPO_ROOT/.temp/e2e 同一处；
 *  .temp 在 worktree 内，天然按 worktree 隔离）。 */
export const E2E_TEMP_ROOT = path.resolve(__dirname, '..', '..', '..', '.temp', 'e2e')
const CURRENT_JSON = path.join(E2E_TEMP_ROOT, 'current.json')

const DEFAULT_BACKEND_URL = 'http://localhost:7102'
const DEFAULT_FRONTEND_URL = 'http://localhost:7002'

/** globalSetup 写出的当前运行快照（current.json）。 */
export interface CurrentRun {
  backendUrl: string
  frontendUrl: string
  token?: string
  e2eRoot: string
  publishDir: string
  /** 宿主数据根（publish/data，小写，输入37 布局）。 */
  dataDir: string
  hostPid?: number
}

function isPidAlive(pid?: number): boolean {
  if (pid == null) return true // 无 PID 记录时不判死（保守采用现有值）
  try {
    process.kill(pid, 0)
    return true
  } catch {
    return false
  }
}

/** 读当前运行快照；不存在 / 字段缺失 / 宿主进程已死 → null（调用方回落默认值）。 */
export function readCurrentRun(): CurrentRun | null {
  if (!existsSync(CURRENT_JSON)) return null
  try {
    const run = JSON.parse(readFileSync(CURRENT_JSON, 'utf8')) as CurrentRun
    if (!run || typeof run.backendUrl !== 'string' || typeof run.frontendUrl !== 'string') return null
    if (!isPidAlive(run.hostPid)) return null
    return run
  } catch {
    return null
  }
}

/** 后端基地址：E2E_BACKEND_URL → current.json → 默认 7102。 */
export function backendUrl(): string {
  return process.env.E2E_BACKEND_URL ?? readCurrentRun()?.backendUrl ?? DEFAULT_BACKEND_URL
}

/** 前端基地址：E2E_FRONTEND_URL → current.json → 默认 7002。 */
export function frontendUrl(): string {
  return process.env.E2E_FRONTEND_URL ?? readCurrentRun()?.frontendUrl ?? DEFAULT_FRONTEND_URL
}

/**
 * 宿主 ForgeSetting.config 绝对路径（真实落盘在 {数据根}/config/ForgeSetting.config，
 * 目录小写 data/config，输入37 布局）。供 token 回退解密 / port-config 恢复端口用。
 */
export function forgeSettingConfigPath(): string {
  const run = readCurrentRun()
  if (run?.dataDir) return path.join(run.dataDir, 'config', 'ForgeSetting.config')
  return process.env.FORGE_SETTING_CONFIG ?? ''
}
