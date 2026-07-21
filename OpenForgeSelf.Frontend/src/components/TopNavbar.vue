<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Grid, Setting } from '@element-plus/icons-vue'

const router = useRouter()
const route = useRoute()

interface NavTab {
  key: string
  label: string
  path: string
}

const allTabs: NavTab[] = [
  { key: 'home', label: '首页', path: '/' },
  { key: 'ai-agent', label: 'AI Agent', path: '/ai-agent' },
  { key: 'skills', label: '技能管理', path: '/skills' },
  { key: 'mcp-tools', label: 'MCP 工具', path: '/mcp-tools' },
  { key: 'system-monitor', label: '系统监控', path: '/system-monitor' },
]

const visiblePaths = ref<string[]>(allTabs.map(t => t.path))

const navTabs = computed(() =>
  allTabs.filter(t => visiblePaths.value.includes(t.path))
)

const activeTab = computed(() => {
  const path = route.path
  for (const tab of navTabs.value) {
    if (path === tab.path) return tab.path
  }
  if (path.startsWith('/agents')) return '/ai-agent'
  if (path.startsWith('/settings')) return ''
  return '/'
})

function navigateTo(path: string): void {
  router.push(path)
}

function closeTab(path: string): void {
  if (path === '/') return
  visiblePaths.value = visiblePaths.value.filter(p => p !== path)
  if (activeTab.value === path) {
    router.push('/')
  }
}
</script>

<template>
  <header class="h-10 flex items-center justify-between px-3 bg-[var(--el-bg-color)] border-b border-[var(--el-border-color)] shrink-0 z-50">
    <!-- 左区：Logo + Tab 标签条 -->
    <div class="flex items-center gap-3 min-w-0">
      <router-link to="/" class="flex items-center gap-2 no-underline shrink-0">
        <svg width="20" height="20" viewBox="0 0 28 28" fill="none" aria-hidden="true">
          <rect x="3" y="14" width="22" height="8" rx="2" fill="var(--el-color-primary)" opacity="0.9" />
          <rect x="6" y="8" width="16" height="8" rx="2" fill="var(--el-color-primary)" opacity="0.7" />
          <rect x="9" y="3" width="10" height="7" rx="2" fill="var(--el-color-primary)" />
          <rect x="12" y="22" width="4" height="3" rx="1" fill="var(--el-text-color-secondary)" />
        </svg>
        <span class="text-[13px] font-bold text-[var(--el-text-color-primary)]">铸己匣</span>
      </router-link>

      <nav class="flex items-center gap-0.5 min-w-0 overflow-x-auto" aria-label="页面标签">
        <button
          v-for="tab in navTabs"
          :key="tab.key"
          class="group flex items-center gap-1.5 px-3 py-1.5 text-[13px] rounded-md transition-colors relative whitespace-nowrap cursor-pointer border-none bg-transparent"
          :class="activeTab === tab.path
            ? 'text-[var(--el-color-primary)] font-semibold'
            : 'text-[var(--el-text-color-regular)] hover:text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color)]'"
          @click="navigateTo(tab.path)"
        >
          <span>{{ tab.label }}</span>
          <span
            v-if="tab.path !== '/'"
            class="hidden group-hover:inline-flex items-center justify-center w-4 h-4 rounded text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color-light)] text-xs leading-none cursor-pointer"
            :title="`关闭 ${tab.label}`"
            @click.stop="closeTab(tab.path)"
          >×</span>
          <!-- 激活态底部指示条 -->
          <span
            v-if="activeTab === tab.path"
            class="absolute bottom-0 left-2 right-2 h-0.5 rounded-full bg-[var(--el-color-primary)]"
          />
        </button>
      </nav>
    </div>

    <!-- 右区：功能按钮 -->
    <div class="flex items-center gap-1 shrink-0">
      <button
        class="flex items-center justify-center w-8 h-8 rounded-md text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color)] transition-colors cursor-pointer border-none bg-transparent"
        title="所有功能"
        aria-label="所有功能"
        @click="router.push('/all-features')"
      >
        <el-icon :size="16"><Grid /></el-icon>
      </button>
      <button
        class="flex items-center justify-center w-8 h-8 rounded-md text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color)] transition-colors cursor-pointer border-none bg-transparent"
        title="设置"
        aria-label="设置"
        @click="router.push('/settings')"
      >
        <el-icon :size="16"><Setting /></el-icon>
      </button>
    </div>
  </header>
</template>
