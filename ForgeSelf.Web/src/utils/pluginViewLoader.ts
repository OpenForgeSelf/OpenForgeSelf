/**
 * 插件界面远程加载器（specs/010-plugin-frontend-runtime）。
 *
 * 职责：按插件清单声明的界面入口，在**运行期**动态加载插件目录下的界面资源，
 * 并把它包装成 Vue 异步组件，使「加载中 / 加载成功 / 加载失败」三态可在路由层表达。
 *
 * 关键点：
 * - 使用运行期 URL 的动态 `import()`，并加 `@vite-ignore` 阻止构建器把它当作
 *   主包内的静态依赖（否则会被静态分析并打包进宿主产物，失去远程加载意义）。
 * - 共享依赖（vue / vue-router / pinia / element-plus）由 index.html 的 import map
 *   解析到宿主实例，插件产物不携带副本，从而避免 Vue 双实例（FR-005）。
 * - 缓存键 = 插件 id + 版本：版本提升即键变化，重新加载，刷新即生效（FR-008）。
 * - 加载失败不抛出到全局，而是渲染错误占位界面，隔离在单条路由内（FR-007 / FR-009）。
 */

import { defineAsyncComponent, defineComponent, h, type Component } from 'vue'

/** 插件界面加载选项。 */
export interface LoadPluginViewOptions {
  /** 插件标识（kebab-case，如 ai-agent）。 */
  pluginId: string
  /** 插件显示名称（用于错误提示）。 */
  pluginName?: string
  /** 插件版本，参与缓存键与资源 URL 的缓存标识。 */
  version: string
  /** 界面资源入口 URL（由 buildPluginAssetUrl 生成）。 */
  entryUrl: string
  /** 插件声明的导出名（views[0]）；缺省时回退到 default 导出。 */
  exportName?: string
  /** 加载超时（毫秒），默认 15 秒。 */
  timeout?: number
}

/** 已解析组件的缓存：键为「插件 id + 版本」。 */
const componentCache = new Map<string, Component>()

/**
 * 注入插件界面样式（幂等）。
 *
 * 背景：Vite 的 lib 模式把 CSS 作为**独立产物**输出，产物 JS 不会引用它，
 * 因此宿主必须自行加载。约定产物目录内的样式文件固定名为 `style.css`
 * （由插件侧 vite 配置 `assetFileNames` 保证）。
 *
 * 去重策略：以「插件 id + 版本」为 data 属性标记，重复加载不会重复插入；
 * 插件卸载/版本变化时由调用方按标记移除。
 *
 * @param entryUrl 界面入口 URL，样式取同目录下的 style.css
 * @param cacheKey 插件缓存键（插件 id + 版本）
 */
function injectPluginStyles(entryUrl: string, cacheKey: string): void {
  const base = entryUrl.split('?')[0]
  const styleUrl = base.slice(0, base.lastIndexOf('/') + 1) + 'style.css'
  const attr = 'data-plugin-style'
  const selector = `link[${attr}="${cacheKey}"]`

  if (document.querySelector(selector)) return

  const link = document.createElement('link')
  link.rel = 'stylesheet'
  link.href = styleUrl
  link.setAttribute(attr, cacheKey)
  document.head.appendChild(link)
}

/**
 * 生成插件界面资源 URL：`/plugins/{插件id}/{入口相对路径}?v={版本}`。
 *
 * 版本作为缓存标识：版本提升即 URL 变化，浏览器自然重新获取，
 * 未提升版本时沿用缓存，避免无谓请求（FR-008）。
 *
 * @param pluginId 插件标识
 * @param entry 清单声明的入口相对路径（如 frontend/index.js）
 * @param version 插件版本；为空时不附加缓存标识
 * @returns 可供浏览器直接请求的资源 URL
 */
export function buildPluginAssetUrl(pluginId: string, entry: string, version?: string): string {
  const normalizedEntry = entry.replace(/^\/+/, '')
  const url = `/plugins/${pluginId}/${normalizedEntry}`
  return version ? `${url}?v=${encodeURIComponent(version)}` : url
}

/** 加载中占位界面。 */
const LoadingView = defineComponent({
  name: 'PluginViewLoading',
  setup() {
    return () =>
      h('div', { class: 'plugin-view-state plugin-view-state--loading' }, [
        h('span', { class: 'plugin-view-state__spinner' }),
        h('span', '正在加载插件界面…'),
      ])
  },
})

/**
 * 加载失败占位界面：展示插件名、资源地址与失败原因，
 * 便于用户与开发者定位问题（FR-007），且不向上传播影响其他路由。
 */
const ErrorView = defineComponent({
  name: 'PluginViewError',
  props: {
    /** 插件显示名称。 */
    pluginLabel: { type: String, required: true },
    /** 资源入口地址。 */
    entryUrl: { type: String, required: true },
    /** 失败原因。 */
    error: { type: null, default: null },
  },
  setup(props) {
    const reason = () => {
      const e = props.error as unknown
      if (e instanceof Error) return e.message
      if (typeof e === 'string' && e.trim()) return e
      return '未知错误'
    }
    return () =>
      h('div', { class: 'plugin-view-state plugin-view-state--error' }, [
        h('h3', '插件界面加载失败'),
        h('p', `插件：${props.pluginLabel}`),
        h('p', `资源：${props.entryUrl}`),
        h('p', `原因：${reason()}`),
      ])
  },
})

/**
 * 远程加载插件界面组件。
 *
 * @param options 加载选项
 * @returns Vue 异步组件（带加载中/失败占位），可直接作为路由 component
 */
export function loadPluginView(options: LoadPluginViewOptions): Component {
  const { pluginId, pluginName, version, entryUrl, exportName, timeout = 15_000 } = options
  const cacheKey = `${pluginId}@${version}`

  const cached = componentCache.get(cacheKey)
  if (cached) return cached

  const component = defineAsyncComponent({
    loader: async (): Promise<Component> => {
      // 产物 JS 不引用自己的 CSS（Vite lib 模式），需宿主按约定注入。
      injectPluginStyles(entryUrl, cacheKey)

      // 运行期 URL 动态导入：@vite-ignore 阻止构建器静态分析此依赖。
      const module = (await import(/* @vite-ignore */ entryUrl)) as Record<string, unknown>

      const name = exportName?.trim()
      const resolved = (name ? module[name] : undefined) ?? module.default
      if (!resolved) {
        throw new Error(
          `插件界面入口未导出可用组件：${entryUrl}${name ? `（期望导出名 ${name}）` : ''}`
        )
      }
      return resolved as Component
    },
    loadingComponent: LoadingView,
    errorComponent: defineComponent({
      name: 'PluginViewErrorHost',
      props: { error: { type: null, default: null } },
      setup(props) {
        return () =>
          h(ErrorView, {
            pluginLabel: pluginName ?? pluginId,
            entryUrl,
            error: props.error,
          })
      },
    }),
    delay: 100,
    timeout,
  })

  componentCache.set(cacheKey, component)
  return component
}

/**
 * 清空插件界面缓存。
 * 插件停用、卸载或版本变化时应调用，避免复用过期组件。
 */
export function clearPluginViewCache(): void {
  componentCache.clear()
  // 同步移除插件样式，避免版本变化后新旧样式叠加
  document.querySelectorAll('link[data-plugin-style]').forEach((el) => el.remove())
}

/** 当前已缓存的插件界面数量（测试与诊断用）。 */
export function getPluginViewCacheSize(): number {
  return componentCache.size
}
