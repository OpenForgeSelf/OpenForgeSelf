<script setup lang="ts">
import { computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Grid, Setting } from '@element-plus/icons-vue'
import { useTabsStore } from '@/stores/tabs'
import { useOpenPage } from '@/composables/useOpenPage'

const router = useRouter()
const route = useRoute()
const tabsStore = useTabsStore()
const { openPage } = useOpenPage()

// 标签栏改为统一从 tabs store 读取（基础标签 + 动态打开的页）
const navTabs = computed(() => tabsStore.tabs)

const activeTab = computed(() => {
  const path = route.path
  const match = navTabs.value.find((t) => t.path === path)
  if (match) return match.path
  if (path.startsWith('/agents')) return '/ai-agent'
  // 动态打开的页（/text-tools 等）已登记在 store.tabs 中，由上面 find 命中
  return ''
})

function navigateTo(path: string): void {
  router.push(path)
}

function closeTab(path: string): void {
  if (path === '/') return
  tabsStore.closeTab(path)
  if (activeTab.value === path) {
    router.push('/')
  }
}
</script>

<template>
  <header class="top-navbar h-10 flex items-center justify-between px-3 bg-[var(--topnav-bg,var(--el-bg-color))] border-b border-[var(--el-border-color)] shrink-0 z-50">
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
        @click="openPage('/all-features', '所有功能')"
      >
        <el-icon :size="16"><Grid /></el-icon>
      </button>
      <button
        class="flex items-center justify-center w-8 h-8 rounded-md text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color)] transition-colors cursor-pointer border-none bg-transparent"
        title="设置"
        aria-label="设置"
        @click="openPage('/settings', '设置')"
      >
        <el-icon :size="16"><Setting /></el-icon>
      </button>
    </div>
  </header>
</template>
