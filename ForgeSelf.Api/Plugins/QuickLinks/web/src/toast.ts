/**
 * 极简 toast 通知（插件自带界面，无第三方依赖）。
 *
 * 用模块级响应式数组作为单一数据源，由 ToastHost.vue 统一渲染；
 * showToast 在任意组件内调用即可，规避原生 alert() 在嵌入宿主/移动端的样式失控。
 */

import { ref } from 'vue'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

export interface ToastItem {
  id: number
  message: string
  type: ToastType
}

/** 全局 toast 列表（ToastHost 渲染此列表） */
export const toasts = ref<ToastItem[]>([])

let seq = 0

/** 关闭指定 toast */
export function dismissToast(id: number): void {
  const idx = toasts.value.findIndex(t => t.id === id)
  if (idx !== -1) toasts.value.splice(idx, 1)
}

/**
 * 弹出一条 toast。
 * @param message 提示文本
 * @param type 类型（影响配色与语义）
 * @param duration 自动关闭毫秒数；<=0 表示常驻（需手动关闭）
 */
export function showToast(message: string, type: ToastType = 'info', duration = 3200): void {
  const id = ++seq
  toasts.value.push({ id, message, type })
  if (duration > 0) {
    setTimeout(() => dismissToast(id), duration)
  }
}
