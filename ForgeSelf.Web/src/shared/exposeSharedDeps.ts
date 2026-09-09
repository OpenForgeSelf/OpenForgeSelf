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
 * 这里导入 Element Plus 的**程序化** API 与**界面组件**（ElButton/ElScrollbar/ElTag/
 * ElProgress/ElSkeleton 等），供插件经 shim（public/shared/element-plus.js）具名再导出。
 * 显式导入会让 vite 把对应组件的 JS + 样式一并打进宿主产物——这正是插件所需：
 * 插件把 element-plus 声明为 external，运行时从 import map 解析到这里拿**同一份**实例与样式。
 */

import * as vue from 'vue'
import * as vueRouter from 'vue-router'
import * as pinia from 'pinia'
/* eslint-disable @typescript-eslint/no-restricted-imports */
// 桥文件豁免：本文件负责把宿主 EP 组件实例暴露给插件 shim（见文件头说明），
// nos-restricted-imports 针对组件模板按需样式注入的约定不适用于此桥接导入。
import {
  ElMessage,
  ElMessageBox,
  ElNotification,
  ElLoading,
  ElButton,
  ElScrollbar,
  ElTag,
  ElProgress,
  ElEmpty,
  ElSkeleton,
  ElSkeletonItem,
} from 'element-plus'
import * as ElementPlusIcons from '@element-plus/icons-vue'
/* eslint-enable @typescript-eslint/no-restricted-imports */

/** 宿主暴露给插件界面的共享依赖集合。 */
export interface ForgeSharedDeps {
  /** Vue 运行时刻模块命名空间。 */
  vue: typeof vue
  /** Vue Router 模块命名空间。 */
  vueRouter: typeof vueRouter
  /** Pinia 模块命名空间。 */
  pinia: typeof pinia
  /** Element Plus 程序化 API + 界面组件（插件经 shim 具名再导出；清单与 public/shared/element-plus.js 保持一致）。 */
  elementPlus: Record<string, unknown>
  /** Element Plus 图标组件命名空间（插件经 element-plus-icons.js shim 具名再导出；与宿主共用同一实例）。 */
  elementPlusIcons: Record<string, unknown>
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
      // 程序化 API
      ElMessage,
      ElMessageBox,
      ElNotification,
      ElLoading,
      // 界面组件（插件模板显式 import 后使用；必须与 public/shared/element-plus.js 清单同步）
      ElButton,
      ElScrollbar,
      ElTag,
      ElProgress,
      ElEmpty,
      ElSkeleton,
      ElSkeletonItem,
    } as unknown as Record<string, unknown>,
    // Element Plus 图标命名空间（shim：public/shared/element-plus-icons.js 具名再导出）
    elementPlusIcons: ElementPlusIcons as unknown as Record<string, unknown>,
  }
}
