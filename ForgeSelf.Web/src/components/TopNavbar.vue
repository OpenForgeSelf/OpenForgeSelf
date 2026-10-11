<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Box, Grid, Picture, Setting } from '@element-plus/icons-vue'
import { useTabsStore } from '@/stores/tabs'
import { useOpenPage } from '@/composables/useOpenPage'
import BackgroundPanel from '@/components/BackgroundPanel.vue'
import AppLogo from '@/components/AppLogo.vue'

const router = useRouter()
const route = useRoute()
const tabsStore = useTabsStore()
const { openPage } = useOpenPage()

// 标签栏统一从 tabs store 读取（基础标签 + 动态打开的页）
const navTabs = computed(() => tabsStore.tabs)

const activeTab = computed(() => {
  const path = route.path
  const match = navTabs.value.find((t) => t.path === path)
  if (match) return match.path
  if (path.startsWith('/agents')) return '/ai-agent'
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

// 背景设置面板开合（DESIGN.md 壳层：右上 300px glass-strong 浮层）
const bgPanelOpen = ref(false)
</script>

<template>
  <header class="topbar shrink-0 z-50">
    <!-- 左区：品牌 + Tab 标签条 -->
    <div class="brand shrink-0">
      <AppLogo :size="28" />
      <span class="name">铸己匣</span>
    </div>

    <nav class="tab-nav min-w-0" aria-label="页面标签">
      <button
        v-for="tab in navTabs"
        :key="tab.key"
        class="tab-item"
        :class="{ active: activeTab === tab.path }"
        @click="navigateTo(tab.path)"
      >
        <span>{{ tab.label }}</span>
        <span
          v-if="tab.path !== '/'"
          class="close-x"
          :title="`关闭 ${tab.label}`"
          @click.stop="closeTab(tab.path)"
        >×</span>
      </button>
    </nav>

    <div class="spacer" />

    <!-- 右区：操作按钮 -->
    <div class="actions shrink-0">
      <button
        title="插件管理"
        aria-label="插件管理"
        @click="openPage('/plugins', '插件管理')"
      >
        <el-icon :size="16"><Box /></el-icon>
      </button>
      <button
        title="所有功能"
        aria-label="所有功能"
        @click="openPage('/all-features', '所有功能')"
      >
        <el-icon :size="16"><Grid /></el-icon>
      </button>
      <button
        title="背景设置"
        aria-label="背景设置"
        :class="{ active: bgPanelOpen }"
        @click="bgPanelOpen = !bgPanelOpen"
      >
        <el-icon :size="16"><Picture /></el-icon>
      </button>
      <button
        title="设置"
        aria-label="设置"
        @click="openPage('/settings', '设置')"
      >
        <el-icon :size="16"><Setting /></el-icon>
      </button>
    </div>

    <BackgroundPanel v-if="bgPanelOpen" @close="bgPanelOpen = false" />
  </header>
</template>

<style scoped>
/* 设计稿 TopBar（kit-styles.css:32-102）：48px 玻璃条，blur 24px 不许调高 */
.topbar {
  height: 48px;
  flex: none;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 0 16px;
  background: var(--glass-bg);
  backdrop-filter: blur(24px) saturate(140%);
  -webkit-backdrop-filter: blur(24px) saturate(140%);
  border-bottom: 1px solid var(--border-hairline);
  position: relative;
}

.brand {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-right: 24px;
}

.brand img {
  width: 22px;
  height: 22px;
}

.brand .name {
  font-family: var(--font-display);
  font-weight: 700;
  font-size: 15px;
  letter-spacing: 0.06em;
  color: var(--fg-primary);
}

.tab-nav {
  display: flex;
  align-items: center;
  gap: 2px;
  overflow-x: auto;
  scrollbar-width: none; /* Firefox */
}

.tab-nav::-webkit-scrollbar {
  display: none; /* Chrome/Safari */
}

.tab-item {
  padding: 6px 14px;
  font-family: var(--font-sans-cn);
  font-size: 13px;
  color: var(--fg-secondary);
  cursor: pointer;
  border-radius: 5px;
  border: none;
  background: transparent;
  transition: color 0.16s var(--ease-out), background 0.16s;
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  letter-spacing: 0.02em;
  white-space: nowrap;
}

.tab-item:hover {
  color: var(--fg-primary);
  background: var(--paper-tint-weak);
}

.tab-item.active {
  color: var(--el-color-primary);
  font-weight: 500;
}

.tab-item.active::after {
  content: '';
  position: absolute;
  left: 12px;
  right: 12px;
  bottom: -1px;
  height: 2px;
  background: var(--el-color-primary);
  border-radius: 2px 2px 0 0;
  box-shadow: var(--gold-glow-md);
}

.close-x {
  display: none;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 4px;
  color: var(--fg-tertiary);
  font-size: 12px;
  line-height: 1;
  cursor: pointer;
}

.tab-item:hover .close-x {
  display: inline-flex;
}

.close-x:hover {
  color: var(--fg-primary);
  background: var(--border-default);
}

.spacer {
  flex: 1;
}

.actions {
  display: flex;
  align-items: center;
  gap: 2px;
}

.actions button {
  width: 30px;
  height: 30px;
  background: transparent;
  border: 0;
  color: var(--fg-tertiary);
  border-radius: 5px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: all 0.16s;
}

.actions button:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.actions button.active {
  color: var(--el-color-primary);
  background: var(--gold-tint-strong);
}
</style>
