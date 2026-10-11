<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import PageHead from '@/components/PageHead.vue'
import EmptyState from '@/components/EmptyState.vue'

interface Tool {
  id: string
  name: string
  icon: string
  description: string
  path: string
  category: 'dev' | 'utils' | 'qa' | 'data'
  tags: string[]
}

const tools: Tool[] = [
  { id: 'code-snippets', name: '代码片段', icon: '📋', description: '常用代码片段管理与复用', path: '/code-snippets', category: 'dev', tags: ['代码'] },
  { id: 'script-runner', name: '脚本运行器', icon: '⚡', description: 'PowerShell / Python / Node.js 脚本在线执行', path: '/script-runner', category: 'dev', tags: ['脚本', '自动化'] },
  { id: 'workflow', name: '工作流引擎', icon: '🔀', description: '可视化工作流编排与执行', path: '/workflows', category: 'dev', tags: ['工作流'] },
  { id: 'api-tester', name: 'API 测试', icon: '🧪', description: 'REST API 在线调试', path: '/dev-tools/api-tester', category: 'qa', tags: ['API'] },
  { id: 'log-viewer', name: '日志查看', icon: '📜', description: '系统运行日志实时查看', path: '/system-logs', category: 'utils', tags: ['日志'] },
  { id: 'file-browser', name: '文件浏览器', icon: '📁', description: '服务器文件管理', path: '/files', category: 'utils', tags: ['文件'] },
  { id: 'db-explorer', name: '数据库浏览', icon: '🗄️', description: 'SQLite 数据库表结构与数据查看', path: '/database-explorer', category: 'data', tags: ['数据库'] },
  { id: 'json-formatter', name: 'JSON 格式化', icon: '{}', description: 'JSON 格式化与校验', path: '/dev-tools/json', category: 'dev', tags: ['JSON'] },
  { id: 'base64', name: 'Base64 编解码', icon: '🔤', description: 'Base64 编码解码工具', path: '/dev-tools/base64', category: 'utils', tags: ['编码'] },
  { id: 'uuid-gen', name: 'UUID 生成器', icon: '🔑', description: '批量生成 UUID/GUID', path: '/dev-tools/uuid', category: 'utils', tags: ['工具'] },
  { id: 'hash-calc', name: '哈希计算', icon: '#️⃣', description: 'MD5 / SHA 哈希计算', path: '/dev-tools/hash', category: 'utils', tags: ['加密'] },
  { id: 'regex-tester', name: '正则测试', icon: '🔍', description: '正则表达式在线测试', path: '/dev-tools/regex', category: 'dev', tags: ['正则'] }
]

const activeTab = ref<'all' | 'dev' | 'utils' | 'qa' | 'data'>('all')
const expandedTools = ref<string[]>([])
const loadError = ref<string>('')

const tabLabels: Record<string, string> = {
  all: '全部工具',
  dev: '开发',
  utils: '实用',
  qa: '测试',
  data: '数据'
}

const filteredTools = computed(() => {
  if (activeTab.value === 'all') return tools
  return tools.filter(t => t.category === activeTab.value)
})

function handleTabChange(tab: 'all' | 'dev' | 'utils' | 'qa' | 'data') {
  activeTab.value = tab
}

function toggleTool(id: string) {
  const idx = expandedTools.value.indexOf(id)
  if (idx >= 0) {
    expandedTools.value.splice(idx, 1)
  } else {
    expandedTools.value.push(id)
  }
}

function isExpanded(id: string) {
  return expandedTools.value.includes(id)
}

function handleExpandAll() {
  expandedTools.value = filteredTools.value.map(t => t.id)
}

function handleCollapseAll() {
  expandedTools.value = []
}

onMounted(async () => {
  try {
    const res = await fetch('/api/DevTools/list')
    if (!res.ok) throw new Error('加载失败')
    const data = await res.json()
    // 后端返回的工具列表可在此合并到本地工具
    if (Array.isArray(data)) {
      // 合并逻辑：保留本地定义的路径与图标，补充后端元数据
    }
  } catch {
    loadError.value = '工具列表加载失败，请检查后端服务'
  }
})
</script>

<template>
  <div class="dev-tools-view">
    <PageHead eyebrow="Dev Tools · 开发者工具" title="工具箱 · 快捷入口">
      <template #actions>
        <button class="btn-ghost" @click="handleExpandAll">
          <svg
            width="13"
            height="13"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <polyline points="7 13 12 18 17 13" /><polyline points="7 6 12 11 17 6" />
          </svg>
          全部展开
        </button>
        <button class="btn-ghost" @click="handleCollapseAll">
          <svg
            width="13"
            height="13"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <polyline points="17 11 12 6 7 11" /><polyline points="17 18 12 13 7 18" />
          </svg>
          全部收起
        </button>
      </template>
    </PageHead>

    <!-- Error banner -->
    <div v-if="loadError" class="error-banner">
      <svg
        width="16"
        height="16"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
        <line x1="12" y1="9" x2="12" y2="13" /><line x1="12" y1="17" x2="12.01" y2="17" />
      </svg>
      <span>{{ loadError }}</span>
      <button class="error-close" aria-label="关闭" @click="loadError = ''">
        <svg
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
        </svg>
      </button>
    </div>

    <!-- Tab navigation -->
    <nav class="tabs-nav" aria-label="工具分类">
      <button
        v-for="(label, key) in tabLabels"
        :key="key"
        class="tab-btn"
        :class="{ active: activeTab === key }"
        @click="handleTabChange(key as 'all' | 'dev' | 'utils' | 'qa' | 'data')"
      >
        {{ label }}
      </button>
    </nav>

    <!-- Tool cards grid -->
    <div v-if="filteredTools.length > 0" class="tools-grid">
      <div
        v-for="tool in filteredTools"
        :key="tool.id"
        class="tool-card"
        :class="{ expanded: isExpanded(tool.id) }"
        @click="toggleTool(tool.id)"
      >
        <div class="tool-icon">{{ tool.icon }}</div>
        <div class="tool-info">
          <h4 class="tool-name">{{ tool.name }}</h4>
          <p class="tool-desc">{{ tool.description }}</p>
        </div>
        <div class="tool-tags">
          <span v-for="tag in tool.tags" :key="tag" class="tool-tag">{{ tag }}</span>
        </div>
      </div>
    </div>

    <EmptyState
      v-else
      title="该分类暂无工具"
      sub="切换到其他分类看看"
    >
      <template #icon>
        <svg
          width="28"
          height="28"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
        </svg>
      </template>
    </EmptyState>
  </div>
</template>

<style scoped>
.dev-tools-view {
  max-width: 1100px;
  margin: 0 auto;
  padding: 24px 32px;
  min-height: 100%;
}

/* Error banner */
.error-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  margin-bottom: 16px;
  background: color-mix(in srgb, var(--cinnabar-400) 8%, transparent);
  border: 1px solid var(--cinnabar-400);
  border-radius: var(--el-border-radius-base);
  color: var(--cinnabar-400);
  font-size: 0.8125rem;
}

.error-close {
  margin-left: auto;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  background: transparent;
  border: none;
  color: var(--cinnabar-400);
  cursor: pointer;
  border-radius: var(--el-border-round);
  transition: background var(--dur-fast) var(--ease-out);
}

.error-close:hover {
  background: color-mix(in srgb, var(--cinnabar-400) 15%, transparent);
}

/* Tabs */
.tabs-nav {
  display: flex;
  gap: 4px;
  margin-bottom: 20px;
  padding: 4px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  width: fit-content;
}

.tab-btn {
  padding: 6px 16px;
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  background: transparent;
  border: none;
  border-radius: var(--el-border-radius-small);
  cursor: pointer;
  transition: all var(--dur-fast) var(--ease-out);
}

.tab-btn:hover {
  color: var(--el-text-color-primary);
  background: var(--el-fill-color-light);
}

.tab-btn.active {
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

/* Tools grid */
.tools-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 16px;
}

.tool-card {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 20px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  cursor: pointer;
  transition: all var(--dur-fast) var(--ease-out);
}

.tool-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: var(--el-box-shadow-light);
  transform: translateY(-2px);
}

.tool-icon {
  font-size: 28px;
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--el-color-primary-light-9);
  border-radius: var(--el-border-radius-base);
}

.tool-info h4 {
  margin: 0 0 4px 0;
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.tool-desc {
  margin: 0;
  font-size: 0.8125rem;
  color: var(--el-text-color-secondary);
  line-height: 1.5;
}

.tool-tags {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.tool-tag {
  font-size: 0.75rem;
  padding: 2px 8px;
  background: var(--el-fill-color);
  color: var(--el-text-color-secondary);
  border-radius: var(--el-border-radius-small);
}

/* Ghost button */
.btn-ghost {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  background: transparent;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  cursor: pointer;
  transition: all var(--dur-fast) var(--ease-out);
}

.btn-ghost:hover {
  color: var(--el-text-color-primary);
  border-color: var(--el-color-primary);
}

@media (max-width: 768px) {
  .dev-tools-view {
    padding: 16px;
  }

  .tools-grid {
    grid-template-columns: 1fr;
  }
}
</style>
