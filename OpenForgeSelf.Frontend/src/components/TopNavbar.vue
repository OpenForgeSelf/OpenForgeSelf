<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'

const router = useRouter()
const route = useRoute()

interface NavTab {
  label: string
  path: string
  icon: string
}

const allTabs: NavTab[] = [
  { label: '首页', path: '/', icon: 'home' },
  { label: 'AI Agent', path: '/ai-agent', icon: 'bot' },
  { label: '提示指令', path: '/prompts', icon: 'file-text' },
  { label: '技能管理', path: '/skills', icon: 'package' },
  { label: 'MCP 工具', path: '/mcp-tools', icon: 'puzzle' },
  { label: '系统监控', path: '/system-monitor', icon: 'activity' },
  { label: '脚本运行器', path: '/code-snippets', icon: 'terminal' },
  { label: '工作流引擎', path: '/workflows', icon: 'git-merge' },
]

// 首页始终保留，其他标签默认全部显示
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
  return '/'
})

function navigateTo(path: string): void {
  router.push(path)
}

function navigateToSettings(): void {
  router.push('/settings')
}

function navigateToAllFeatures(): void {
  router.push('/all-features')
}

function closeTab(path: string): void {
  if (path === '/') return // 首页不可关闭
  visiblePaths.value = visiblePaths.value.filter(p => p !== path)
  // 如果关闭的是当前活跃标签，回首页
  if (activeTab.value === path) {
    router.push('/')
  }
}
</script>

<template>
  <header class="top-navbar">
    <!-- Left: Logo -->
    <div class="navbar-logo">
      <svg
        width="20"
        height="20"
        viewBox="0 0 28 28"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
        aria-hidden="true"
        class="logo-icon"
      >
        <rect
          x="3"
          y="14"
          width="22"
          height="8"
          rx="2"
          fill="var(--primary-color)"
          opacity="0.9"
        />
        <rect
          x="6"
          y="8"
          width="16"
          height="8"
          rx="2"
          fill="var(--primary-color)"
          opacity="0.7"
        />
        <rect
          x="9"
          y="3"
          width="10"
          height="7"
          rx="2"
          fill="var(--primary-color)"
        />
        <rect
          x="12"
          y="22"
          width="4"
          height="3"
          rx="1"
          fill="var(--text-muted)"
        />
      </svg>
      <span class="brand-name">铸己匣</span>
    </div>

    <!-- Center: Tab Strip -->
    <nav class="navbar-tabs" aria-label="页面导航">
      <button
        v-for="tab in navTabs"
        :key="tab.path"
        class="nav-tab"
        :class="{ active: activeTab === tab.path }"
        @click="navigateTo(tab.path)"
      >
        <!-- Home icon -->
        <svg
          v-if="tab.icon === 'home'"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
          <polyline points="9 22 9 12 15 12 15 22" />
        </svg>
        <!-- Bot icon -->
        <svg
          v-else-if="tab.icon === 'bot'"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <rect
            x="3"
            y="11"
            width="18"
            height="10"
            rx="2"
          />
          <circle cx="12" cy="5" r="2" />
          <path d="M9 14h6" />
          <path d="M12 14v4" />
        </svg>
        <!-- File-text icon -->
        <svg
          v-else-if="tab.icon === 'file-text'"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <polyline points="14 2 14 8 20 8" />
          <line x1="16" y1="13" x2="8" y2="13" />
          <line x1="16" y1="17" x2="8" y2="17" />
          <polyline points="10 9 9 9 8 9" />
        </svg>
        <!-- Package icon -->
        <svg
          v-else-if="tab.icon === 'package'"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <line x1="16.5" y1="9.4" x2="7.5" y2="4.21" />
          <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
          <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
          <line x1="12" y1="22.08" x2="12" y2="12" />
        </svg>
        <!-- Puzzle icon -->
        <svg
          v-else-if="tab.icon === 'puzzle'"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M19.5 3H16a2 2 0 0 0-2 2v.5a.5.5 0 0 1-.5.5h-3a.5.5 0 0 1-.5-.5V5a2 2 0 0 0-2-2H4.5A2.5 2.5 0 0 0 2 5.5V8a2 2 0 0 0 2 2h.5a.5.5 0 0 1 .5.5v3a.5.5 0 0 1-.5.5H4a2 2 0 0 0-2 2v2.5A2.5 2.5 0 0 0 4.5 19H7a2 2 0 0 0 2-2v-.5a.5.5 0 0 1 .5-.5h3a.5.5 0 0 1 .5.5V17a2 2 0 0 0 2 2h2.5a2.5 2.5 0 0 0 2.5-2.5V14a2 2 0 0 0-2-2h-.5a.5.5 0 0 1-.5-.5v-3a.5.5 0 0 1 .5-.5H17a2 2 0 0 0 2-2V5.5A2.5 2.5 0 0 0 19.5 3z" />
        </svg>
        <!-- Activity icon -->
        <svg
          v-else-if="tab.icon === 'activity'"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
        </svg>
        <!-- Terminal icon -->
        <svg
          v-else-if="tab.icon === 'terminal'"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <polyline points="4 17 10 11 4 5" />
          <line x1="12" y1="19" x2="20" y2="19" />
        </svg>
        <!-- Git-merge icon -->
        <svg
          v-else-if="tab.icon === 'git-merge'"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <circle cx="18" cy="18" r="3" />
          <circle cx="6" cy="6" r="3" />
          <path d="M6 21V9a9 9 0 0 0 9 9" />
        </svg>
        <span class="tab-label">{{ tab.label }}</span>
        <!-- Close button for non-home tabs -->
        <span
          v-if="tab.path !== '/'"
          class="nav-tab-close"
          :title="`关闭 ${tab.label}`"
          @click.stop="closeTab(tab.path)"
        >
          <svg
            width="12"
            height="12"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </span>
      </button>
    </nav>

    <!-- Right: Action Buttons -->
    <div class="navbar-actions">
      <button class="features-btn" title="全部功能" @click="navigateToAllFeatures">
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <rect x="3" y="3" width="7" height="7" />
          <rect x="14" y="3" width="7" height="7" />
          <rect x="14" y="14" width="7" height="7" />
          <rect x="3" y="14" width="7" height="7" />
        </svg>
        <span>功能</span>
      </button>
      <button class="action-btn" title="设置" @click="navigateToSettings">
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <circle cx="12" cy="12" r="3" />
          <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
        </svg>
      </button>
    </div>
  </header>
</template>

<style scoped>
.top-navbar {
  display: flex;
  align-items: center;
  height: 40px;
  padding: 0 var(--space-4);
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
  flex-shrink: 0;
  z-index: 50;
  gap: var(--space-4);
}

.navbar-logo {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-shrink: 0;
}

.logo-icon {
  display: block;
}

.brand-name {
  font-family: var(--font-family-base);
  font-size: 0.8125rem;
  font-weight: 700;
  color: var(--text-primary);
  white-space: nowrap;
}

.navbar-tabs {
  display: flex;
  align-items: center;
  gap: 0;
  flex: 1;
  overflow-x: auto;
  scrollbar-width: none;
  -ms-overflow-style: none;
}

.navbar-tabs::-webkit-scrollbar {
  display: none;
}

.nav-tab {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  height: 40px;
  padding: 0 var(--space-3);
  border: none;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--text-secondary);
  font-family: var(--font-family-base);
  font-size: 0.8125rem;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition:
    color var(--motion-fast),
    border-color var(--motion-fast),
    background-color var(--motion-fast);
  flex-shrink: 0;
}

.nav-tab:hover {
  color: var(--text-primary);
  background: var(--bg-hover);
}

.nav-tab.active {
  color: var(--text-primary);
  border-bottom-color: var(--primary-color);
}

.nav-tab.active svg {
  color: var(--primary-color);
}

.tab-label {
  line-height: 1;
}

/* 标签关闭按钮 — 仅非首页标签显示 */
.nav-tab-close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: var(--radius-sm);
  color: var(--text-muted);
  opacity: 0.5;
  flex-shrink: 0;
  transition: opacity var(--motion-fast), background-color var(--motion-fast), color var(--motion-fast);
}

.nav-tab:hover .nav-tab-close {
  opacity: 1;
  color: var(--text-secondary);
}

.nav-tab-close:hover {
  background: var(--bg-elevated);
  color: var(--danger-color) !important;
}

.navbar-actions {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  flex-shrink: 0;
}

.action-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  transition:
    background-color var(--motion-fast),
    color var(--motion-fast);
}

.action-btn:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.features-btn {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  height: 28px;
  padding: 0 var(--space-3);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--text-secondary);
  font-family: var(--font-family-base);
  font-size: 0.8125rem;
  font-weight: 500;
  cursor: pointer;
  transition:
    background-color var(--motion-fast),
    color var(--motion-fast),
    border-color var(--motion-fast);
  flex-shrink: 0;
}

.features-btn:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
  border-color: var(--text-muted);
}

.features-btn svg {
  display: block;
}

.action-btn svg {
  display: block;
}
</style>