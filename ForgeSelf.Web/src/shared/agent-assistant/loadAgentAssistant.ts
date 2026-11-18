import { defineAsyncComponent, defineComponent, h, type Component } from 'vue'
import { usePluginManifestStore } from '@/stores/pluginManifest'
import { buildPluginAssetUrl, buildPluginStyleUrl } from '@/utils/pluginViewLoader'

/**
 * 加载智能体插件导出的 AgentAssistant。
 * 不用 loadPluginView 的默认缓存键（不含 exportName），避免和整页 AiAgentView 撞缓存。
 */
export function loadAgentAssistant(): Component {
  return defineAsyncComponent({
    loader: async () => {
      const store = usePluginManifestStore()
      if (!store.loaded) await store.loadManifest()
      const plugin = store.manifest.find((m) => m.id === 'ai-agent')
      const entry = plugin?.frontend?.entry
      if (!plugin || !entry) throw new Error('未找到智能体插件界面入口')
      const assetVersion = plugin.webVersion ?? plugin.version ?? ''
      const entryUrl = buildPluginAssetUrl(plugin.id, entry, assetVersion)
      const styleUrl = buildPluginStyleUrl(entryUrl, assetVersion)
      const attr = 'data-plugin-style'
      // 样式与整页共用同一份 CSS；缓存键带导出名称，避免和 loadPluginView 撞上 AiAgentView
      const key = `${plugin.id}@${assetVersion}`
      if (!document.querySelector(`link[${attr}="${key}"]`)) {
        const link = document.createElement('link')
        link.rel = 'stylesheet'
        link.href = styleUrl
        link.setAttribute(attr, key)
        document.head.appendChild(link)
      }
      const mod = (await import(/* @vite-ignore */ entryUrl)) as Record<string, unknown>
      const resolved = mod.AgentAssistant ?? mod.default
      if (!resolved) throw new Error('智能体插件未导出 AgentAssistant')
      return resolved as Component
    },
    loadingComponent: defineComponent({
      setup: () => () => h('div', { class: 'plugin-view-state' }, '正在加载助手…'),
    }),
  })
}
