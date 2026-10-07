import ChatRecordsView from '@/views/ChatRecordsView.vue'
import { createRouter, createWebHistory } from 'vue-router'
import PluginStore from '@/views/PluginStore.vue'
import PluginDetail from '@/views/PluginDetail.vue'
import PluginUpdates from '@/views/PluginUpdates.vue'
import PluginImportExport from '@/views/PluginImportExport.vue'
import PluginScaffolder from '@/views/PluginScaffolder.vue'
import MemoryView from '@/views/MemoryView.vue'
import ChatView from '@/views/ChatView.vue'
import TextToolsView from '@/views/TextToolsView.vue'
import DevToolsView from '@/views/DevToolsView.vue'
import ScriptLibrary from '@/views/ScriptLibrary.vue'
import AgentsManageView from '@/views/AgentsManageView.vue'
import AllFeaturesView from '@/views/AllFeaturesView.vue'
import ProfileView from '@/views/ProfileView.vue'
import PromptsView from '@/views/PromptsView.vue'
import SkillsView from '@/views/SkillsView.vue'
import SettingsView from '@/views/SettingsView.vue'
import SystemMonitorView from '@/views/SystemMonitorView.vue'
import CodeSnippetsView from '@/views/CodeSnippetsView.vue'
import WorkflowLibrary from '@/views/WorkflowLibrary.vue'
import type { PluginFrontendManifest, PluginMenuItem } from '@/types/plugin'
import { registerPluginRoutes } from './pluginRoutes'
import { registerManifestRoutes } from './dynamicPlugins'
/**
 * 首页重定向目标：由 main.ts 在预取阶段（loadManifest 后）解析并写入。
 * vue-router 的 `redirect` 类型不支持返回 Promise，故此处仅同步读取缓存值，
 * 真正的异步解析（settings + manifest）放在 main.ts 的 beforeEach 里完成。
 */
let _homeRedirectTarget: string | null = null
export function setHomeRedirectTarget(route: string | null): void {
  _homeRedirectTarget = route
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      // 首页已全量插件化（Home 插件，id=home）。宿主 `/` 不再绑定任何内置组件，
      // 而是按 ForgeSetting.HomePluginId 查已启用插件 manifest 的 frontend.route 作重定向目标，
      // 查不到（插件未启用/缺失）时回退 `/home`。目标路由由 main.ts 预取阶段解析后
      // 经 setHomeRedirectTarget 写入，此处仅同步读取（vue-router redirect 不支持 Promise 返回）。
      path: '/',
      name: 'home',
      redirect: () => _homeRedirectTarget ?? '/home',
    },
    // 注意：/ai-agent 与 /quick-links 已**不再**由宿主静态路由提供。
    // 两个页面均已彻底迁移到插件自带界面（Plugins/{AIAgent,QuickLinks}/web），
    // 由插件清单声明的 route 经 registerManifestRoutes 动态注册到对应路径。
    // 插件未加载/未启用时该路径走 404 兜底，不再有宿主内置实现。
    {
      path: '/prompts',
      name: 'prompts',
      component: PromptsView
    },
    {
      path: '/skills',
      name: 'skills',
      component: SkillsView
    },
    {
      path: '/settings',
      name: 'settings',
      component: SettingsView
    },
    {
      path: '/memory',
      name: 'memory',
      component: MemoryView
    },
    {
      path: '/agents',
      name: 'agents',
      component: AgentsManageView
    },
    {
      path: '/all-features',
      name: 'all-features',
      component: AllFeaturesView
    },
    {
      path: '/system-monitor',
      name: 'system-monitor',
      component: SystemMonitorView
    },
    {
      path: '/code-snippets',
      name: 'code-snippets',
      component: CodeSnippetsView
    },
    {
      path: '/workflows',
      name: 'workflows',
      component: WorkflowLibrary
    },
    {
      path: '/profile',
      name: 'profile',
      component: ProfileView
    },
    {
      path: '/plugins',
      name: 'plugins',
      component: PluginStore
    },
    {
      path: '/plugins/:id',
      name: 'plugin-detail',
      component: PluginDetail
    },
    {
      path: '/plugins/updates',
      name: 'plugin-updates',
      component: PluginUpdates
    },
    {
      path: '/plugins/import-export',
      name: 'plugin-import-export',
      component: PluginImportExport
    },
    {
      path: '/plugins/scaffolder',
      name: 'plugin-scaffolder',
      component: PluginScaffolder
    },
    {
      path: '/chat-records',
      name: 'chat-records',
      component: ChatRecordsView
    },
    {
      path: '/chat',
      name: 'chat',
      component: ChatView
    },
    {
      path: '/text-tools',
      name: 'text-tools',
      component: TextToolsView
    },
    {
      path: '/dev-tools',
      name: 'dev-tools',
      component: DevToolsView
    },
    {
      path: '/script-runner',
      name: 'script-runner',
      component: ScriptLibrary
    }
  ]
})

export function setupPluginRoutes(menuItems: PluginMenuItem[]): void {
  registerPluginRoutes(router, menuItems)
}

/**
 * 清单驱动的动态视图挂载：根据后端前端清单（route + views）注册懒加载路由。
 * 清单失败/为空时 registerManifestRoutes 不注入任何路由，回退到静态路由表。
 */
export function setupManifestRoutes(manifest: PluginFrontendManifest[]): void {
  registerManifestRoutes(router, manifest)
}

export default router
