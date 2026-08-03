import ChatRecordsView from '@/views/ChatRecordsView.vue'
import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'
import PluginStore from '@/views/PluginStore.vue'
import PluginDetail from '@/views/PluginDetail.vue'
import PluginUpdates from '@/views/PluginUpdates.vue'
import PluginImportExport from '@/views/PluginImportExport.vue'
import PluginScaffolder from '@/views/PluginScaffolder.vue'
import MemoryView from '@/views/MemoryView.vue'
import AgentView from '@/views/AgentView.vue'
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
import type { PluginMenuItem } from '@/types/plugin'
import { registerPluginRoutes } from './pluginRoutes'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView
    },
    {
      path: '/ai-agent',
      name: 'ai-agent',
      component: AgentView
    },
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
    }
  ]
})

export function setupPluginRoutes(menuItems: PluginMenuItem[]): void {
  registerPluginRoutes(router, menuItems)
}

export default router
