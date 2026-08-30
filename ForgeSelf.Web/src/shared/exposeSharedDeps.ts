/**
 * 宿主共享依赖桥（specs/010-plugin-frontend-runtime）。
 *
 * 作用：把宿主真实的模块命名空间挂到 `window.__FORGE_SHARED__`，
 * 供 `public/shared/*.js` 的 shim 再导出。插件产物把 vue / vue-router / pinia /
 * element-plus 声明为外部依赖，运行时经 `index.html` 的 import map 解析到这些 shim，
 * 从而与宿主共用**同一份**模块实例，杜绝 Vue 双实例导致的响应式失效（FR-005）。
 *
 * 为什么需要这层桥：ESM 不支持动态再导出（无法 `export * from window.xxx`），
 * 必须由一段真实模块把宿主实例暴露出来供 shim 具名再导出。
 *
 * 注意：本文件是宿主启动期的桥接模块，**不适用**「组件内禁止显式 import ElXxx」的
 * 约定——那条约定针对组件内绕过 unplugin-vue-components 按需样式注入的**组件**导入；
 * 这里只导入 Element Plus 的**程序化** API，界面组件仍由宿主全局注册供插件直接使用。
 */

import * as vue from 'vue'
import * as vueRouter from 'vue-router'
import * as pinia from 'pinia'
import { ElMessage, ElMessageBox, ElNotification, ElLoading } from 'element-plus'

/** 宿主暴露给插件界面的共享依赖集合。 */
export interface ForgeSharedDeps {
  /** Vue 运行时刻模块命名空间。 */
  vue: typeof vue
  /** Vue Router 模块命名空间。 */
  vueRouter: typeof vueRouter
  /** Pinia 模块命名空间。 */
  pinia: typeof pinia
  /** Element Plus 程序化 API 子集（界面组件走宿主全局注册，不在此提供）。 */
  elementPlus: Record<string, unknown>
}

declare global {
  interface Window {
    /** 宿主共享依赖；由 exposeSharedDeps() 在启动期挂载。 */
    __FORGE_SHARED__?: ForgeSharedDeps
  }
}

/**
 * 挂载共享依赖到全局。
 *
 * MUST 在任何插件界面加载之前调用（main.ts 顶部、清单路由注册之前），
 * 否则插件界面加载时 shim 会因取不到实例而抛错。
 */
export function exposeSharedDeps(): void {
  window.__FORGE_SHARED__ = {
    vue,
    vueRouter,
    pinia,
    elementPlus: {
      ElMessage,
      ElMessageBox,
      ElNotification,
      ElLoading,
    } as unknown as Record<string, unknown>,
  }
}
