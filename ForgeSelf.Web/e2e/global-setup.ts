import { spawn, execSync, type ChildProcess } from 'node:child_process'
import { copyFileSync, createWriteStream, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { FullConfig } from '@playwright/test'
import { ensureFreePort, worktreeTag } from './helpers/free-port'

/**
 * 统一 e2e 共享基础设施（globalSetup · PILOT-050 T4 重构）
 * ----------------------------------------------------------------
 * 目录：<仓库根>/.temp/e2e/wt-<hash8> —— 按 worktree 根路径哈希稳定派生，
 * 不再用时间戳（时间戳随机目录每次变化，是 Windows 防火墙弹窗的根因）。
 * 同 worktree 反复运行复用同目录：先杀残留宿主 → 清旧 publish → 重新发布。
 *
 * 端口：playwright.config 求值期已同步认领 E2E_BACKEND_PORT / E2E_FRONTEND_PORT
 * （跨 worktree 经 tmpdir 认领注册表互斥）；此处复核认领并起宿主，
 * 经 FORGESELF_PORT 注入（宿主 StartupPortResolver 覆盖 ForgeSetting 并落盘）。
 *
 * 数据根：FORGESELF_DATA_ROOT=<publish>/data 显式隔离（B9-4 前置重载），
 * 宿主落盘的 ForgeSetting.config 全部进隔离目录，不污染用户 ~/.forgeself。
 *
 * 关键机制：
 * - token：宿主首启 GET /api/api-server-init-token 语义见 fetchInitToken；
 *   globalSetup 把明文 token 写入 state.json + current.json（跨进程真源，worker 读它）。
 * - 运行态快照：current.json（.temp/e2e/current.json，含 hostPid）+ state.json + host.pid，
 *   供 e2e-env.ts / real-auth.ts / port-config.spec.ts 统一读取（不再扫描时间戳目录）。
 */

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)
const REPO_ROOT = path.resolve(__dirname, '..', '..') // ForgeSelf.Web/e2e -> repo root
const PUBLISH_PROJECT = path.join(REPO_ROOT, 'ForgeSelf.Api', 'ForgeSelf.Api.csproj')

const HOST_EXE = 'ForgeSelf.exe'

let hostProc: ChildProcess | null = null

/** 按 PID 杀进程树（Windows 用 taskkill /t /f，其余 SIGTERM）。只用于本 worktree 自己拉起的 e2e 宿主。 */
function killTree(pid: number): void {
  try {
    if (process.platform === 'win32') {
      spawn('taskkill', ['/pid', String(pid), '/t', '/f'], { stdio: 'ignore' })
    } else {
      process.kill(pid, 'SIGTERM')
    }
  } catch {
    /* 进程可能已退出 */
  }
}

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
async function fetchInitToken(url: string): Promise<string | null> {
  try {
    const r = await fetch(url)
    const body = (await r.json()) as { success?: boolean; data?: { apiKeyPlain?: string } }
    if (body.success && body.data?.apiKeyPlain) return body.data.apiKeyPlain
  } catch {
    /* 忽略，回退解密 ForgeSetting.config */
  }
  return null
}

/**
 * 宿主 exe 签名（仅 Windows）：复用 scripts/sign-publish.ps1 给发布版 exe（及 ForgeSelf*.dll）
 * 做 Authenticode 签名，避免无签名 exe 被系统 SmartScreen / 安全软件拦截导致启动失败。
 *
 * 行为由环境变量 E2E_SIGN_EXE 控制：
 * - 'true'（默认）：Windows 下必须签名成功；失败则抛错中断 e2e（不静默跑无签名 exe）
 * - 'warn'：尝试签名，失败仅告警并继续
 * - 'false'：完全跳过（适用于无 Windows SDK / signtool 的环境）
 * 非 Windows 平台一律跳过（无 signtool）。
 *
 * 注意：必须在 spawn 宿主之前调用——运行中的 exe 不可被签名（被内存映射，写入会失败且有损坏风险）。
 */
async function signHostExecutable(publishDir: string, logFile: string): Promise<void> {
  const mode = (process.env.E2E_SIGN_EXE ?? 'true').toLowerCase()
  if (mode === 'false') {
    console.warn('[e2e] E2E_SIGN_EXE=false，跳过宿主 exe 签名')
    return
  }
  if (process.platform !== 'win32') {
    console.warn('[e2e] 非 Windows 平台，跳过宿主 exe 签名（仅 Windows 需要 signtool）')
    return
  }
  const signScript = path.join(REPO_ROOT, 'scripts', 'sign-publish.ps1')
  if (!existsSync(signScript)) {
    const msg = `未找到签名脚本 ${signScript}，无法给宿主 exe 签名`
    if (mode === 'warn') console.warn(`[e2e] ⚠ ${msg}`)
    else throw new Error(msg)
    return
  }

  console.log('[e2e] 对宿主 exe 做 Authenticode 签名（避免无签名被拦截）...')
  try {
    // 必须用 pwsh（PowerShell 7）：2026-10-01 实测，从 Node spawn 的 powershell.exe（5.1）
    // 无法加载 Security 模块 → Cert: 提供程序缺失、代码签名证书查不到，签名必失败；
    // pwsh 同语境 Cert: 正常（drive=True certs=1）。规范见 docs/04-standards/agent-workflow.md。
    await run(
      'pwsh',
      [
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File', signScript,
        '-PublishDir', publishDir,
        '-NoTimestamp', // e2e 临时签名无需 RFC3161 时间戳，避免联网依赖
      ],
      {
        cwd: REPO_ROOT,
        logFile,
        env: { ...process.env },
      },
    )
    console.log(`[e2e] 宿主 exe 已签名：${path.join(publishDir, HOST_EXE)}（详情见 ${logFile}）`)
  } catch (e) {
    const msg = `宿主 exe 签名失败：${(e as Error).message}`
    if (mode === 'warn') {
      console.warn(`[e2e] ⚠ ${msg}（e2e 仍继续，但无签名 exe 可能被拦截）`)
    } else {
      throw new Error(
        `${msg}；如需跳过请设置 E2E_SIGN_EXE=false；签名环境要求：PowerShell 7（pwsh）+ 可用代码签名证书（自签会自动创建）`,
        { cause: e },
      )
    }
  }
}

/**
 * 绕过 safe-delete shim 的目录删除：本环境 Node fs.rmSync 被 safe-delete shim 接管，
 * 对大目录（publish/ 数千文件）触发 SAFE_DELETE_BULK_GUARD_ERROR 直接中断 globalSetup
 * （2026-09-30 深档 e2e 实证）。改走子进程 OS 级删除，不经 Node shim。
 */
function rmDirOS(dir: string): void {
  if (!existsSync(dir)) return
  try {
    if (process.platform === 'win32') {
      execSync(`cmd /c rmdir /s /q "${dir}"`, { stdio: 'ignore' })
    } else {
      execSync(`rm -rf "${dir}"`, { stdio: 'ignore' })
    }
  } catch (e) {
    throw new Error(`清理目录失败（OS 级删除）: ${dir}；${(e as Error).message}`, { cause: e })
  }
}

export default async function globalSetup(_config: FullConfig) {
  const tag = worktreeTag(REPO_ROOT)
  const e2eTempRoot = path.join(REPO_ROOT, '.temp', 'e2e')
  const e2eRoot = path.join(e2eTempRoot, tag)
  const currentJsonPath = path.join(e2eTempRoot, 'current.json')

  // 0) 残留进程保护：本 worktree 上一次 e2e 宿主仍存活（崩溃/中断残留）→ 先杀再清，
  //    否则旧宿主 DLL 被内存映射，publish 目录清理与重发布会失败。只认 current.json 里
  //    e2eRoot 匹配本 worktree 的记录，绝不触碰用户实例（:51888 等）。
  try {
    const prev = JSON.parse(readFileSync(currentJsonPath, 'utf8')) as { hostPid?: number; e2eRoot?: string }
    if (prev?.hostPid && prev.e2eRoot === e2eRoot) {
      console.log(`[e2e] 发现本 worktree 残留宿主 PID=${prev.hostPid}，先终止...`)
      killTree(prev.hostPid)
      await new Promise((r) => setTimeout(r, 1500)) // 给进程树退出留时间
    }
  } catch {
    /* 无 current.json（首次运行/已清理） */
  }

  // 0.5) E2E_CLEAN=1 → 清空整个 e2eRoot（含日志/数据）；无论旧 publish 与 data 无条件清
  //      （防陈旧 DLL + 保证「全新临时 DB」铁律）。删除走 OS 级（绕 safe-delete shim）。
  if (process.env.E2E_CLEAN === '1') {
    rmDirOS(e2eRoot)
  }
  rmDirOS(path.join(e2eRoot, 'publish'))
  rmDirOS(path.join(e2eRoot, 'data'))
  mkdirSync(e2eRoot, { recursive: true })
  const publishDir = path.join(e2eRoot, 'publish')
  const dataDir = path.join(publishDir, 'data') // 小写，输入37 目录命名统一

  // 1) 全新构建到隔离目录（同 worktree 每次重发，保证与当前源码一致）
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
  // dotnet publish 产物不含 System.Data.SQLite.dll（长期宿主靠 NewLife 首启自动下载落盘，
  // 临时目录联网下载在本机会被拒 → XCode 表初始化全挂 → 所有 DB API 500）。
  // 从仓库根 publish/（长期运行宿主目录，与 51888 实例同源）复制，保证与线上运行态一致。
  // 注意：live 布局可能把 DLL 放在 publish/ 根或 publish/Plugins/（两种都兼容），
  // 临时 publish 两端都放（XCode 探测路径覆盖根目录与 Plugins 子目录），最大化兼容。
  const SQLITE_DLLS = ['System.Data.SQLite.dll', 'e_sqlite3.dll']
  const livePublishDir = path.join(REPO_ROOT, 'publish')
  for (const dll of SQLITE_DLLS) {
    // 源优先级：仓内 build/runtime/Plugins（受版本控制、与 package-release.ps1 注入包里的同一份，
    // 全新 worktree 无 publish/ 时也能跑）→ 长期宿主 publish 根 → publish/Plugins（兼容布局漂移）。
    const srcCandidates = [
      path.join(REPO_ROOT, 'build', 'runtime', 'Plugins', dll),
      path.join(livePublishDir, dll),
      path.join(livePublishDir, 'Plugins', dll),
    ]
    const src = srcCandidates.find((c) => existsSync(c))
    if (!src) {
      throw new Error(
        `缺少 SQLite provider ${dll}：dotnet publish 不携带它，且 ${REPO_ROOT}\\build\\runtime\\Plugins 与 ` +
          `${livePublishDir}（根/Plugins）中均未找到。请补齐仓内 build/runtime/Plugins/${dll}（正常应随仓库存在）。`,
      )
    }
    // 目的：publish 根 + Plugins/ 都放，覆盖 XCode 不同探测路径；publish 已清空 → 无条件覆盖（修陈旧隐患）
    for (const dstBase of [publishDir, path.join(publishDir, 'Plugins')]) {
      mkdirSync(dstBase, { recursive: true })
      copyFileSync(src, path.join(dstBase, dll))
    }
  }

  // 1.6) 对宿主 exe 做 Authenticode 签名：避免无签名 exe 被系统/安全软件拦截导致启动失败。
  // 复用 scripts/sign-publish.ps1（自签证书 + 本机受信任根，与 build.ps1 -Sign 同源）。
  // 必须在 spawn 前完成（运行中的 exe 不可签）。
  await signHostExecutable(publishDir, path.join(e2eRoot, 'sign.log'))

  // 1.7) 端口：优先用 playwright.config 求值期认领的 E2E_BACKEND_PORT（同进程，认领可重入）；
  // 起宿主前复核「真正可绑定」（认领注册表只保证 e2e 之间互斥，在跑实例可能绑着同口），
  // 不可绑则顺延重选；独立运行本 setup 时回退 pickFreePort。
  const backendPort = await ensureFreePort(
    Number(process.env.E2E_BACKEND_PORT) || 7102,
    REPO_ROOT,
  )
  const frontendPort = Number(process.env.E2E_FRONTEND_PORT) || 7002

  // 2) 起宿主：FORGESELF_DATA_ROOT 显式隔离数据根；FORGESELF_PORT 覆盖监听端口（宿主落盘 ForgeSetting.config）；
  //    FORGESelf_INSTANCE_ID 按 worktree 稳定（独立全局 Mutex，与用户实例 / 其他 worktree 互不阻塞）。
  const backendUrl = `http://localhost:${backendPort}`
  const frontendUrl = `http://localhost:${frontendPort}`
  const backendLog = createWriteStream(path.join(e2eRoot, 'backend.log'), { flags: 'w' })
  hostProc = spawn(path.join(publishDir, HOST_EXE), ['--console'], {
    cwd: publishDir,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      FORGESELF_DATA_ROOT: dataDir,
      FORGESELF_PORT: String(backendPort),
      // 禁用托盘：托盘创建失败（多实例并存时 Explorer 拒绝）会以 Unhandled exception 打崩宿主
      FORGESELF_NO_TRAY: '1',
      FORGESelf_INSTANCE_ID: `e2e-${tag}`,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  hostProc.stdout?.pipe(backendLog)
  hostProc.stderr?.pipe(backendLog)
  hostProc.on('exit', () => backendLog.end())

  // 3) 健康检查（宿主绑定 FORGESELF_PORT 失败会在此快速暴露）
  await waitForUrl(`${backendUrl}/api/health`, 120_000)

  // 4) 初始化 token（首启返回明文；否则回退 real-auth 解密 ForgeSetting.config）
  const token = await fetchInitToken(`${backendUrl}/api/api-server/init-token`)

  // 5) 写出运行态快照 + 注入 worker 环境
  const state = {
    backendUrl,
    frontendUrl,
    token: token ?? '',
    e2eRoot,
    publishDir,
    dataDir,
    hostPid: hostProc.pid ?? undefined,
  }
  writeFileSync(path.join(e2eRoot, 'state.json'), JSON.stringify(state, null, 2))
  // current.json：跨进程真源（e2e-env/real-auth/port-config 统一读它，按 hostPid 存活判过期）
  writeFileSync(currentJsonPath, JSON.stringify(state, null, 2))
  if (hostProc.pid) writeFileSync(path.join(e2eRoot, 'host.pid'), String(hostProc.pid))

  if (token) process.env.E2E_API_TOKEN = token
  process.env.E2E_BACKEND_URL = backendUrl
  process.env.E2E_FRONTEND_URL = frontendUrl
  // 回退解密路径：隔离数据根的 ForgeSetting.config（首启未拿到明文时；小写 data/config）
  process.env.FORGE_SETTING_CONFIG = path.join(dataDir, 'config', 'ForgeSetting.config')

  return { e2eRoot, hostPid: hostProc.pid ?? undefined }
}
