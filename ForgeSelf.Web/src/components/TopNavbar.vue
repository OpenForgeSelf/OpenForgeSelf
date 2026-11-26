<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Box, Grid, Picture, Setting } from '@element-plus/icons-vue'
import { useTabsStore } from '@/stores/tabs'
import { useRuntimeBoardStore } from '@/stores/runtimeBoard'
import { useOpenPage } from '@/composables/useOpenPage'
import BackgroundPanel from '@/components/BackgroundPanel.vue'
import AppLogo from '@/components/AppLogo.vue'
// 钉住常驻标签：与首页「常用功能」/「所有功能」页共用同一份使用度数据（pinned）
import { useUsageStatsStore } from '@/stores/usageStats'
import { usePluginManifestStore } from '@/stores/pluginManifest'
import { mergeFeatureList } from '@/data/features'
import { featureUsageKey } from '@/data/homeEntries'
import { lucideIconSvg } from '@/data/featureIcons'
import type { NavTab } from '@/stores/tabs'

const router = useRouter()
const route = useRoute()
const tabsStore = useTabsStore()
const runtimeBoard = useRuntimeBoardStore()
const { openPage } = useOpenPage()
const usageStore = useUsageStatsStore()
const manifestStore = usePluginManifestStore()

// 标签栏统一从 tabs store 读取（基础标签 + 动态打开的页）
const navTabs = computed(() => tabsStore.tabs)

/**
 * 钉住常驻标签：来自使用度 store 的 pinned 列表（键=path，与统计口径一致）。
 * - 数据单一来源：所有功能页卡片「钉住」/ 此处图钉取消，改的是同一份 pinned；
 * - 只展示有可跳转路由的功能（无页面功能如仅后端插件没有标签可开）；
 * - 插在「首页」之后、动态标签之前，不可通过 × 关闭（取消钉住走图钉按钮）。
 */
const pinnedTabs = computed<NavTab[]>(() => {
  const byKey = new Map(mergeFeatureList(manifestStore.menus).map((f) => [featureUsageKey(f), f]))
  return usageStore.pinned
    .map((key) => {
      const f = byKey.get(key)
      return f?.path ? { key: `pin:${f.path}`, label: f.name, path: f.path } : null
    })
    .filter((t): t is NavTab => !!t && t.path !== '/')
})

/** 展示用标签序列：首页 → 钉住常驻 → 其余动态标签（钉住项若已被动态打开则去重，只保留常驻位）。 */
const displayTabs = computed<NavTab[]>(() => {
  const home = navTabs.value.filter((t) => t.path === '/')
  const dynamic = navTabs.value.filter((t) => t.path !== '/')
  const pinnedPaths = new Set(pinnedTabs.value.map((t) => t.path))
  return [...home, ...pinnedTabs.value, ...dynamic.filter((t) => !pinnedPaths.has(t.path))]
})

const activeTab = computed(() => {
  const path = route.path
  const match = displayTabs.value.find((t) => t.path === path)
  if (match) return match.path
  if (path.startsWith('/agents')) return '/ai-agent'
  return ''
})

/** 该标签是否为钉住常驻项（常驻项不显示 × 关闭，改显示图钉；点图钉 = 取消钉住）。 */
const pinnedPathSet = computed(() => new Set(pinnedTabs.value.map((t) => t.path)))

function isPinnedTab(tab: NavTab): boolean {
  return pinnedPathSet.value.has(tab.path)
}

/**
 * 页签默认 logo（2026-11-25 输入28 追加轮）：按 path 在功能单一真源（内置 + 插件清单）
 * 里匹配 FeatureItem，用 lucide 内联 SVG（featureIcons.ts 单一源，与「所有功能」页同表）。
 * 无匹配（如 /all-features 等工具页）返回空串，槽留空；悬浮时槽内动作图标照常互换。
 */
const logoByPath = computed(() => {
  const m = new Map<string, string>()
  for (const f of mergeFeatureList(manifestStore.menus)) {
    if (f.path) m.set(f.path, lucideIconSvg(f.icon, 14))
  }
  return m
})

function tabLogo(path: string): string {
  return logoByPath.value.get(path) ?? ''
}

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

/** 取消钉住：从常驻序列移除；若该页此前未打开，标签随之消失。 */
function unpin(path: string): void {
  usageStore.togglePin(path)
}

function badgeOf(tab: NavTab): number | undefined {
  return tabsStore.tabs.find((t) => t.path === tab.path)?.badge
}

onMounted(() => {
  // 使用度 store 懒加载：进站即读一次，保证钉住标签在刷新后立即恢复
  usageStore.load()
  // 运行实况的阻塞数字跟标签走，不依赖当前是否停在这一页
  runtimeBoard.start()
})

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
        v-for="tab in displayTabs"
        :key="tab.key"
        class="tab-item"
        :class="{ active: activeTab === tab.path }"
        @click="navigateTo(tab.path)"
      >
        <!-- 左侧 16px 图标槽：默认 logo，悬浮同位互换动作图标。运行实况的阻塞数字跟在文字后面，不进这个槽。 -->
        <span v-if="tab.path !== '/'" class="tab-icon-slot" aria-hidden="true">
          <span class="tab-logo" v-html="tabLogo(tab.path)"></span>
          <span
            v-if="isPinnedTab(tab)"
            class="close-x"
            :title="`取消钉住 ${tab.label}`"
            @click.stop="unpin(tab.path)"
          >📌</span>
          <span
            v-else
            class="close-x"
            :title="`关闭 ${tab.label}`"
            @click.stop="closeTab(tab.path)"
          >×</span>
        </span>
        <span class="tab-label">{{ tab.label }}</span>
        <span v-if="badgeOf(tab)" class="tab-badge">{{ badgeOf(tab) }}</span>
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
  color: var(--el-text-color-primary);
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
  padding: 6px 9px; /* 水平 14→9（2026-11-25 输入28：页签左右边距调小；相邻页签文字间距 30→20px） */
  font-family: var(--el-font-family);
  font-size: 13px;
  color: var(--el-text-color-regular);
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
  color: var(--el-text-color-primary);
  background: var(--paper-tint-weak);
}

.tab-item.active {
  color: var(--el-color-primary);
  font-weight: 500;
}

.tab-item.active::after {
  content: '';
  position: absolute;
  left: 7px; /* 与 padding 9px 维持「下划线比文字宽 2px」既有关系（原 12/14） */
  right: 7px;
  bottom: -1px;
  height: 2px;
  background: var(--el-color-primary);
  border-radius: 2px 2px 0 0;
  box-shadow: var(--gold-glow-md);
}

.tab-badge {
  min-width: 16px;
  padding: 0 4px;
  border-radius: var(--el-border-radius-small);
  background: var(--gold-400);
  color: var(--ink-950);
  font-size: 11px;
  line-height: 16px;
  text-align: center;
}

/* 左侧 16px 图标槽：默认 logo、悬浮同位互换动作图标。
   槽与两图标同一 16px 盒、盒宽恒定，显隐只走 visibility/opacity。 */
.tab-icon-slot {
  position: relative;
  width: 16px;
  height: 16px;
  flex: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.tab-logo {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--el-text-color-secondary);
  visibility: visible;
  opacity: 1;
  /* 悬出：logo 立即淡出；移回：延迟翻转 visibility 保证淡入完整 */
  transition:
    opacity var(--dur-fast) var(--ease-out),
    visibility 0s linear var(--dur-fast);
}

.tab-logo :deep(svg) {
  display: block;
}

.tab-item:hover .tab-logo,
.tab-item:focus-within .tab-logo {
  visibility: hidden;
  opacity: 0;
  transition:
    opacity var(--dur-fast) var(--ease-out),
    visibility 0s;
}

.close-x {
  position: absolute;
  inset: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  visibility: hidden;
  opacity: 0;
  border-radius: 4px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 1;
  cursor: pointer;
  /* 悬入立即显现 + 160ms 淡入；移出延迟翻转 visibility 保证淡出完整 */
  transition:
    opacity var(--dur-fast) var(--ease-out),
    visibility 0s linear var(--dur-fast);
}

.tab-item:hover .close-x,
.tab-item:focus-within .close-x {
  visibility: visible;
  opacity: 1;
  transition:
    opacity var(--dur-fast) var(--ease-out),
    visibility 0s;
}

.close-x:hover {
  color: var(--el-text-color-primary);
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
  color: var(--el-text-color-secondary);
  border-radius: 5px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: all 0.16s;
}

.actions button:hover {
  background: var(--paper-tint);
  color: var(--el-text-color-primary);
}

.actions button.active {
  color: var(--el-color-primary);
  background: var(--gold-tint-strong);
}
</style>
