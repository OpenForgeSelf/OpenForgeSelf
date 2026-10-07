/**
 * 插件自带的轻提示与确认框（不依赖宿主的 Element Plus 桥）。
 *
 * 为什么不用 `ElMessage` / `ElMessageBox`：宿主确实经
 * `ForgeSelf.Web/public/shared/element-plus.js` 暴露了这两个 API，但该桥是**手工维护的清单**，
 * 取不到时导出为 `undefined`（桥文件自述）。插件界面一旦调它就整块崩在交互那一刻，
 * 排查成本远高于自己写 40 行。QuickLinks 走的也是这条路。
 *
 * 两条硬要求（plugin-development §3.2/§3.4）：
 * 1. 凡"点一下就改变用户可见状态"的动作都要二次确认，且软标记类文案要写明"数据保留 + 在哪撤销"；
 * 2. 确认逻辑写成**可单测的纯编排函数**（`confirmAction` 只返回 Promise<boolean>，
 *    「用户取消 ⇒ 一个请求都不发」这条语义能被 vitest 直接锁死，不依赖任何弹窗实现）。
 */
import { ref } from 'vue'

export type ToastKind = 'success' | 'error' | 'warning' | 'info'

export interface ToastItem {
  id: number
  message: string
  kind: ToastKind
}

/** 提示列表（NotifyHost 渲染此数组） */
export const toasts = ref<ToastItem[]>([])

let seq = 0
const timers = new Map<number, ReturnType<typeof setTimeout>>()

/** 同时最多显示 3 条：提示是给人读的，不是堆叠计数（实测 4 条「已保存」会把刚点过的控件整片盖住）。 */
const MaxVisible = 3

function forget(id: number): void {
  const t = timers.get(id)
  if (t) {
    clearTimeout(t)
    timers.delete(id)
  }
}

function remove(id: number): void {
  forget(id)
  const idx = toasts.value.findIndex(t => t.id === id)
  if (idx >= 0) toasts.value.splice(idx, 1)
}

export function dismissToast(id: number): void {
  remove(id)
}

/**
 * 弹一条轻提示。
 * @param duration 自动关闭毫秒；<=0 常驻（错误类默认 6s，够读完 reason）
 */
export function showToast(message: string, kind: ToastKind = 'info', duration = kind === 'error' ? 6000 : 3200): void {
  const text = message || '（空提示）'
  // 同文案同类型只保留一条并重置计时：连续失焦保存会连发「已保存」，
  // 叠 4 条正好压在详情面板右半（实测截图挡住了「交给 AgentHub 执行」按钮）。
  const dup = toasts.value.find(t => t.message === text && t.kind === kind)
  if (dup) {
    if (duration > 0) {
      forget(dup.id)
      timers.set(dup.id, setTimeout(() => remove(dup.id), duration))
    }
    return
  }

  const id = ++seq
  toasts.value.push({ id, message: text, kind })
  while (toasts.value.length > MaxVisible) remove(toasts.value[0].id)
  if (duration > 0) timers.set(id, setTimeout(() => remove(id), duration))
}

/** 失败提示的统一出口：把后端 reason 原文端出来，用户才知道下一步改什么。 */
export function showFailure(action: string, error: unknown): void {
  const message = error instanceof Error ? error.message : String(error)
  console.warn(`[todo-tracker] ${action} 失败:`, error)
  showToast(`${action}失败：${message}`, 'error')
}

export interface ConfirmOptions {
  title?: string
  /** 纯文本提示（渲染时不解析 HTML，避免注入） */
  message: string
  detail?: string
  confirmText?: string
  cancelText?: string
  /** 危险操作：确认键渲染为红色 */
  danger?: boolean
}

export const confirmVisible = ref(false)
export const confirmOptions = ref<ConfirmOptions>({ message: '' })

let resolver: ((ok: boolean) => void) | null = null

/** 请求确认；true=用户确认。同一时刻只允许一个确认框（后到的直接被判取消，不排队叠加）。 */
export function confirmAction(options: ConfirmOptions): Promise<boolean> {
  if (confirmVisible.value && resolver) {
    const pending = resolver
    resolver = null
    pending(false)
  }
  confirmOptions.value = options
  confirmVisible.value = true
  return new Promise<boolean>(resolve => {
    resolver = resolve
  })
}

/** 由 NotifyHost 的按钮调用：兑现 Promise 并关闭对话框 */
export function resolveConfirm(ok: boolean): void {
  confirmVisible.value = false
  const r = resolver
  resolver = null
  if (r) r(ok)
}
