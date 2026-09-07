/**
 * 自定义确认对话框（替代原生 confirm()）。
 *
 * confirmAction() 返回 Promise<boolean>，由 ConfirmHost.vue 渲染对话框并调用
 * resolveConfirm 兑现。这样在嵌入宿主/移动端也能保持与插件一致的样式，
 * 且支持传入「级联影响」富文本提示（如删除分类将连带删除 N 条链接）。
 */

import { ref } from 'vue'

export interface ConfirmOptions {
  title?: string
  /** 纯文本提示（不解析 HTML，避免注入） */
  message: string
  confirmText?: string
  cancelText?: string
  /** 是否为危险操作（确认按钮渲染为红色） */
  danger?: boolean
}

export const confirmVisible = ref(false)
export const confirmOptions = ref<ConfirmOptions>({ message: '' })

let resolver: ((ok: boolean) => void) | null = null

/** 弹出确认框，返回用户选择（true=确认） */
export function confirmAction(options: ConfirmOptions): Promise<boolean> {
  confirmOptions.value = options
  confirmVisible.value = true
  return new Promise<boolean>((resolve) => {
    resolver = resolve
  })
}

/** 由 ConfirmHost 调用，兑现 Promise 并关闭对话框 */
export function resolveConfirm(ok: boolean): void {
  confirmVisible.value = false
  const r = resolver
  resolver = null
  if (r) r(ok)
}
