/**
 * 「二次确认 → 执行」编排（sems 界面所有会改变用户可见状态的动作共用）。
 *
 * 为什么要抽这一层：
 * - 项目技能约定（plugin-development §3.2 / §3.4）：凡点一下就会改变用户可见状态的动作
 *   （移除项目、删命令、停进程）必须二次确认，且**确认逻辑要能被单测直接锁死**。
 * - 本模块**不依赖任何 UI 框架**：确认动作由调用方注入（生产传 ElMessageBox 版，单测传假实现），
 *   于是「用户取消 → 一个请求都不发」这条关键语义可以用 vitest 断言，而不必在测试里弹真窗。
 * - 界面原先直接用 window.confirm/window.alert：既没有统一文案，Playwright 也得走 dialog 拦截，
 *   取消路径与确认路径难以分别断言。
 */

/** 确认回调：true=用户确认，false=取消。 */
export type ConfirmFn = (message: string, title: string) => Promise<boolean>

/** 编排结果：已执行 / 用户取消 / 执行失败（带错误文案）。 */
export type ConfirmOutcome = 'done' | 'cancelled' | 'failed'

export interface ConfirmResult {
  outcome: ConfirmOutcome
  /** 仅 outcome==='failed' 时存在，供界面提示。 */
  error?: string
}

export interface ConfirmOptions {
  title: string
  message: string
  confirm: ConfirmFn
  action: () => Promise<unknown>
}

/**
 * 先确认、后请求。用户取消时**不发起任何网络请求**（回归守卫：确认绝不允许被跳过）。
 * 失败时把异常转成中文文案，交由界面留痕展示（§3.4「操作成败可见」）。
 */
export async function runWithConfirm(options: ConfirmOptions): Promise<ConfirmResult> {
  const confirmed = await options.confirm(options.message, options.title)
  if (!confirmed) return { outcome: 'cancelled' }

  try {
    await options.action()
    return { outcome: 'done' }
  } catch (e) {
    return { outcome: 'failed', error: errorMessage(e) }
  }
}

/** 异常 → 可读文案。 */
export function errorMessage(e: unknown): string {
  return e instanceof Error ? e.message : String(e)
}

/**
 * 移除项目的确认文案：必须写明「只删档案、不动磁盘」，
 * 否则用户会误以为连工程文件一起删掉。
 */
export function removeProjectMessage(name: string, commandCount: number): string {
  const cmd = commandCount > 0 ? `，并级联删除其 ${commandCount} 条运行命令记录` : ''
  return (
    `确认移除项目「${name}」的档案${cmd}？\n\n` +
    '只删除 sems 里的项目档案与命令记录，不会删除磁盘上的目录与文件；' +
    '移除后如需恢复，重新「添加项目」选择同一目录即可（档案本身不可撤销）。'
  )
}

/** 删除命令的确认文案。 */
export function deleteCommandMessage(name: string): string {
  return `确认删除命令「${name}」？只删这条命令记录，不影响磁盘文件与已在运行的进程，且不可恢复。`
}

/** 停止本面板启动的会话的确认文案。 */
export function stopSessionMessage(projectName: string, commandName: string, pid: number): string {
  return `确认停止「${projectName} / ${commandName}」（PID ${pid}）？将强制结束该进程树，不可恢复。`
}

/**
 * 停止外部捕获进程的确认文案：必须明示「杀整棵树可能含他人进程」，
 * 这类进程不是本面板启动的，误停影响面由调用方承担。
 */
export function stopExternalMessage(projectName: string, commandName: string, pid: number): string {
  return (
    `确认停止外部进程「${projectName} / ${commandName}」（PID ${pid}）？\n\n` +
    '该进程由本机检测捕获、并非本面板启动。停止会杀整棵进程树，可能波及他人进程且不可恢复，请先确认归属。'
  )
}
