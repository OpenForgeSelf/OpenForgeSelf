import { spawn, type ChildProcess } from 'node:child_process'
import { copyFileSync, createWriteStream, existsSync, mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { FullConfig } from '@playwright/test'

/**
 * 统一 e2e 共享基础设施（globalSetup）
 * ----------------------------------------------------------------
 * 每次运行：把宿主构建产物发布到独立时间戳临时目录（无 DLL 锁冲突、全新干净），
 * 以临时目录作数据目录启动整套环境（宿主 7102 + 前端 dev 7002），
 * 保证每次测试互不冲突、且不污染用户 ~/.forgeself（全新环境）。
 *
 * 关键机制：
 * - 数据目录隔离：用 ASPNETCORE_ENVIRONMENT=Development 跑发布版 exe，
 *   宿主数据根 = 发布目录/Data（每次 temp 全新），天然避开用户数据目录。
 * - token：宿主启动即幂等生成并加密存储 ApiToken；首启 GET /api/api-server/init-token
 *   返回明文（无鉴权），globalSetup 注入 worker 环境 E2E_API_TOKEN。
 * - 端口：宿主固定 7102（项目约定，与既有 28 个应用层 spec + vite 代理一致）。
 */

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)
const REPO_ROOT = path.resolve(__dirname, '..', '..') // ForgeSelf.Web/e2e -> repo root
const PUBLISH_PROJECT = path.join(REPO_ROOT, 'ForgeSelf.Api', 'ForgeSelf.Api.csproj')

const BACKEND_PORT = 7102
const FRONTEND_PORT = 7002
const HOST_EXE = 'ForgeSelf.exe'
const HEALTH_URL = `http://localhost:${BACKEND_PORT}/api/health`
const INIT_TOKEN_URL = `http://localhost:${BACKEND_PORT}/api/api-server/init-token`

let hostProc: ChildProcess | null = null

/** 运行外部命令，stdout/stderr 落盘，非零退出抛错。 */
function run(
  cmd: string,
  args: string[],
  opts: { cwd: string; logFile: string; env?: NodeJS.ProcessEnv },
): Promise<void> {
  return new Promise((resolve, reject) => {
    const log = createWriteStream(opts.logFile, { flags: 'w' })
    const p = spawn(cmd, args, {
      cwd: opts.cwd,
      env: opts.env ?? process.env,
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    p.stdout?.pipe(log)
    p.stderr?.pipe(log)
    p.on('error', (e) => {
      log.end()
      reject(e)
    })
    p.on('close', (code) => {
      log.end()
      if (code === 0) resolve()
      else reject(new Error(`${cmd} ${args.join(' ')} 退出码 ${code}；详见 ${opts.logFile}`))
    })
  })
}

/** 轮询 URL 直到 200，超时抛错。 */
async function waitForUrl(url: string, timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs
  for (;;) {
    try {
      const r = await fetch(url)
      if (r.ok) return
    } catch {
      /* 尚未就绪 */
    }
    if (Date.now() > deadline) {
      throw new Error(`等待 ${url} 就绪超时（${timeoutMs}ms）；详见 backend.log`)
    }
    await new Promise((r) => setTimeout(r, 1000))
  }
}

/** 首启初始化 token（无鉴权），返回明文；已初始化则返回 null。 */
async function fetchInitToken(): Promise<string | null> {
  try {
    const r = await fetch(INIT_TOKEN_URL)
    const body = (await r.json()) as { success?: boolean; data?: { apiKeyPlain?: string } }
    if (body.success && body.data?.apiKeyPlain) return body.data.apiKeyPlain
  } catch {
    /* 忽略，回退解密 ForgeSetting.config */
  }
  return null
}

export default async function globalSetup(_config: FullConfig) {
  const ts = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19)
  const e2eRoot = path.join(REPO_ROOT, '.temp', 'e2e', ts)
  mkdirSync(e2eRoot, { recursive: true })
  const publishDir = path.join(e2eRoot, 'publish')

  // 1) 全新构建到临时目录（每次干净、无锁冲突）
  await run(
    'dotnet',
    ['publish', PUBLISH_PROJECT, '-c', 'Release', '-o', publishDir, '--nologo', '-v', 'minimal'],
    {
      cwd: REPO_ROOT,
      logFile: path.join(e2eRoot, 'backend-build.log'),
      env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' },
    },
  )

  // 1.5) 补齐 SQLite provider（NewLife XCode 运行时依赖）：
  // dotnet publish 产物不含 System.Data.SQLite.dll（长期宿主靠 NewLife 首启自动下载落盘到 publish/，
  // 临时目录联网下载在本机会被拒 → XCode 表初始化全挂 → 所有 DB API 500）。
  // 从仓库根 publish/（长期运行宿主目录，与 51888 实例同源）复制，保证与线上运行态一致。
  const SQLITE_DLLS = ['System.Data.SQLite.dll', 'e_sqlite3.dll']
  const livePublishDir = path.join(REPO_ROOT, 'publish')
  for (const dll of SQLITE_DLLS) {
    const dst = path.join(publishDir, dll)
    if (existsSync(dst)) continue
    const src = path.join(livePublishDir, dll)
    if (!existsSync(src)) {
      throw new Error(
        `缺少 SQLite provider ${dll}：dotnet publish 不携带它，且 ${livePublishDir} 中也未找到。` +
          `请确保本机长期运行宿主目录（publish/）内存在该 DLL（NewLife 首启自动下载会落盘于此）。`,
      )
    }
    copyFileSync(src, dst)
  }

  // 2) 起宿主：Development 环境 → 数据根 = publish/Data（全新，隔离 ~/.forgeself）
  const backendLog = createWriteStream(path.join(e2eRoot, 'backend.log'), { flags: 'w' })
  hostProc = spawn(path.join(publishDir, HOST_EXE), ['--console'], {
    cwd: publishDir,
    // FORGESelf_INSTANCE_ID：让 e2e 宿主持有独立全局 Mutex，与用户开发实例（PID 32956 等）互不阻塞
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      FORGESelf_INSTANCE_ID: `e2e-${ts}`,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  hostProc.stdout?.pipe(backendLog)
  hostProc.stderr?.pipe(backendLog)
  hostProc.on('exit', () => backendLog.end())

  // 3) 健康检查
  await waitForUrl(HEALTH_URL, 120_000)

  // 4) 初始化 token（首启返回明文；否则回退 real-auth 解密 ForgeSetting.config）
  const token = await fetchInitToken()

  // 5) 写出 state + 注入 worker 环境
  const state = {
    backendUrl: `http://localhost:${BACKEND_PORT}`,
    frontendUrl: `http://localhost:${FRONTEND_PORT}`,
    token: token ?? '',
    e2eRoot,
    publishDir,
    dataDir: path.join(publishDir, 'Data'),
  }
  writeFileSync(path.join(e2eRoot, 'state.json'), JSON.stringify(state, null, 2))

  if (token) process.env.E2E_API_TOKEN = token
  process.env.E2E_BACKEND_URL = state.backendUrl
  // 回退解密路径：临时数据目录的 ForgeSetting.config（首启未拿到明文时）
  process.env.FORGE_SETTING_CONFIG = path.join(publishDir, 'Data', 'Config', 'ForgeSetting.config')

  return { e2eRoot, hostPid: hostProc.pid ?? undefined }
}
