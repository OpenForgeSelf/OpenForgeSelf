import ChatRecordsView from '@/views/ChatRecordsView.vue'
import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'
import PluginStore from '@/views/PluginStore.vue'
import PluginDetail from '@/views/PluginDetail.vue'
import PluginUpdates from '@/views/PluginUpdates.vue'
import PluginImportExport from '@/views/PluginImportExport.vue'
import PluginScaffolder from '@/views/PluginScaffolder.vue'
import MemoryView from '@/views/MemoryView.vue'
import ChatView from '@/views/ChatView.vue'
import TextToolsView from '@/views/TextToolsView.vue'
import FileToolsView from '@/views/FileToolsView.vue'
import DevToolsView from '@/views/DevToolsView.vue'
import ScriptLibrary from '@/views/ScriptLibrary.vue'
import AgentsManageView from '@/views/AgentsManageView.vue'
import AllFeaturesView from '@/views/AllFeaturesView.vue'
import ProfileView from '@/views/ProfileView.vue'
import PromptsView from '@/views/PromptsView.vue'
import SkillsView from '@/views/SkillsView.vue'
import McpToolsView from '@/views/McpToolsView.vue'
import SettingsView from '@/views/SettingsView.vue'
import SystemMonitorView from '@/views/SystemMonitorView.vue'
import CodeSnippetsView from '@/views/CodeSnippetsView.vue'
import WorkflowLibrary from '@/views/WorkflowLibrary.vue'
import TodoView from '@/views/TodoView.vue'
import CaptureView from '@/views/CaptureView.vue'
import type { PluginFrontendManifest, PluginMenuItem } from '@/types/plugin'
import { registerPluginRoutes } from './pluginRoutes'
import { registerManifestRoutes } from './dynamicPlugins'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView
    },
    // 注意：/ai-agent 与 /quick-links 已**不再**由宿主静态路由提供。
    // 两个页面均已彻底迁移到插件自带界面（ForgeSelf.Api/Plugins/{AIAgent,QuickLinks}/web），
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
      path: '/mcp-tools',
      name: 'mcp-tools',
      component: McpToolsView
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
      path: '/todo',
      name: 'todo',
      component: TodoView
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
      path: '/file-tools',
      name: 'file-tools',
      component: FileToolsView
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
    },
    {
      path: '/capture',
      name: 'capture',
      component: CaptureView
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
