/// <reference types="vite/client" />

/** 构建期由 vite.config.ts define 注入，来源 = ../plugin.json 的 Version 字段（版本串单一来源） */
declare const __PLUGIN_VERSION__: string

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<object, object, unknown>
  export default component
}
