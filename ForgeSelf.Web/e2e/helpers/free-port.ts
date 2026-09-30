/**
 * e2e 端口分配与跨 worktree 认领（PILOT-050 T2）
 * ----------------------------------------------------------------
 * 目标：多 worktree 并行跑 e2e 时不抢端口、不误用对方宿主。
 *
 * 机制（三层）：
 * 1. 稳定派生：目录名 / 首选端口由 worktree 根路径哈希派生（同 worktree 每次一致，
 *    不再用时间戳——时间戳随机目录是 Windows 防火墙弹窗的根因）。
 * 2. 认领注册表：`${os.tmpdir()}/forgeself-e2e-ports/<port>.lock`，`openSync('wx')`
 *    独占认领；锁内容含 PID + cwd + 时间戳。持有者进程已死 / cwd 已删 / 锁超过 2h
 *    任一满足即视为陈旧，可被重新认领。
 * 3. 绑定探测：认领成功后再用 net server 试绑定一次，绑定失败顺延下一端口
 *    （防系统保留段 / 极小概率竞态）。
 *
 * 说明：前端 dev server 端口在 playwright.config 求值期需同步确定，Node 无同步绑定探测，
 * 故前端走「派生 + claim」；若真被占用，由 vite strictPort 快速失败暴露，不静默换口。
 */
import { createHash } from 'node:crypto'
import net from 'node:net'
import os from 'node:os'
import path from 'node:path'
import { closeSync, existsSync, mkdirSync, openSync, readFileSync, statSync, unlinkSync, writeSync } from 'node:fs'

/** 端口认领注册表目录（跨 worktree 共享）。 */
const CLAIM_DIR = path.join(os.tmpdir(), 'forgeself-e2e-ports')
/** 认领锁老化阈值：超过即视为陈旧（宿主异常退出未清理的兜底）。 */
const LOCK_TTL_MS = 2 * 60 * 60 * 1000

/** 由 worktree 根路径派生稳定目录/标识：同 worktree 恒定，跨 worktree 互异。 */
export function worktreeTag(root: string): string {
  const abs = path.resolve(root)
  const hash = createHash('sha256').update(abs).digest('hex').slice(0, 8)
  return `wt-${hash}`
}

function lockPath(port: number): string {
  return path.join(CLAIM_DIR, `${port}.lock`)
}

function isPidAlive(pid: number): boolean {
  try {
    process.kill(pid, 0)
    return true
  } catch {
    return false
  }
}

/** 该端口是否被「仍然存活的认领者」占着：PID 死 / cwd 消失 / 锁超龄 → 陈旧，不算占用。 */
function claimHeldByLiveOwner(port: number): boolean {
  const p = lockPath(port)
  if (!existsSync(p)) return false
  try {
    let info: { pid?: number; cwd?: string } = {}
    try {
      info = JSON.parse(readFileSync(p, 'utf8')) as { pid?: number; cwd?: string }
    } catch {
      info = {}
    }
    if (Date.now() - statSync(p).mtimeMs > LOCK_TTL_MS) return false
    if (info.pid === process.pid) return false // 本进程先前的认领 → 视为可重入（config 与 globalSetup 同进程）
    if (info.pid == null && info.cwd == null) return false // 无法归属 → 陈旧
    if (info.pid != null && !isPidAlive(info.pid)) return false
    if (info.cwd != null && !existsSync(info.cwd)) return false
    return true
  } catch {
    return false
  }
}

/**
 * 同步认领端口（供 playwright.config 求值期使用）。
 * 返回 true = 认领成功；false = 被存活认领者占用（或并发抢锁失败）。
 */
export function claimPortSync(port: number, ownerCwd: string): boolean {
  mkdirSync(CLAIM_DIR, { recursive: true })
  if (claimHeldByLiveOwner(port)) return false
  try {
    unlinkSync(lockPath(port))
  } catch {
    /* 陈旧锁不存在/已清 */
  }
  try {
    const fd = openSync(lockPath(port), 'wx')
    writeSync(fd, JSON.stringify({ pid: process.pid, cwd: path.resolve(ownerCwd), at: new Date().toISOString() }))
    closeSync(fd)
    return true
  } catch {
    return false // 并发窗口内被别的进程抢到
  }
}

/** 释放本进程对端口的认领（宿主正常退出 / globalTeardown 时调用）。 */
export function releasePort(port: number): void {
  try {
    unlinkSync(lockPath(port))
  } catch {
    /* 忽略 */
  }
}

function isBindable(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const srv = net.createServer()
    srv.once('error', () => resolve(false))
    srv.listen({ port, host: '127.0.0.1' }, () => {
      srv.close(() => resolve(true))
    })
  })
}

/**
 * 确保端口真正可绑定：认领成功且当前可绑定 → 原样返回；
 * 被其他存活认领者占用 / 认领了但绑不上（在跑实例占用等）→ 释放并从 preferred+1 起顺延重选。
 * 供 globalSetup 起宿主前复核（认领注册表只保证 e2e 之间互斥，不管在跑实例占用）。
 */
export async function ensureFreePort(preferred: number, ownerCwd: string): Promise<number> {
  if (claimPortSync(preferred, ownerCwd)) {
    if (await isBindable(preferred)) return preferred
    releasePort(preferred)
  }
  return pickFreePort(preferred + 1, ownerCwd)
}

/**
 * 从 preferred 起顺延找一个「未被存活认领者占用 且 当前可绑定」的端口。
 * 用于 globalSetup（异步上下文）给后端宿主选口；选中后经 FORGESELF_PORT 注入宿主。
 */
export async function pickFreePort(preferred: number, ownerCwd: string, maxTries = 50): Promise<number> {
  for (let i = 0; i < maxTries; i++) {
    const port = preferred + i
    if (port > 65535) break
    if (!claimPortSync(port, ownerCwd)) continue
    if (await isBindable(port)) return port
    releasePort(port) // 认领了但绑不上（系统保留/竞态）→ 换下一个
  }
  throw new Error(`pickFreePort: 自 ${preferred} 起连续 ${maxTries} 个端口均不可用`)
}
