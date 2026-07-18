<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { mcpApi } from '@/services/mcpApi'
import ForgeSwitch from '@/components/forge/ForgeSwitch.vue'
import type { McpServerDto, McpToolDto, McpTestResultDto } from '@/types/mcp'

const servers = ref<McpServerDto[]>([])
const tools = ref<McpToolDto[]>([])
const selectedServerId = ref<string | null>(null)
const keyword = ref('')
const activeCategory = ref('全部')
const testResult = ref<{ toolId: string; result: McpTestResultDto } | null>(null)
const testLoading = ref<string | null>(null)
const toggleLoading = ref<string | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const categories = ['全部', '系统', '文件', '网络', '数据', '开发']

const filteredTools = computed(() => {
  let list = tools.value
  if (activeCategory.value !== '全部') {
    list = list.filter(t => t.category === activeCategory.value)
  }
  if (keyword.value.trim()) {
    const kw = keyword.value.trim().toLowerCase()
    list = list.filter(t =>
      t.name.toLowerCase().includes(kw) ||
      t.description.toLowerCase().includes(kw)
    )
  }
  return list
})

const serverStatusColor = (status: string): string => {
  switch (status) {
    case 'connected': return 'var(--brand-amber-500)'
    case 'disconnected': return 'var(--text-muted)'
    case 'error': return 'var(--danger-color)'
    default: return 'var(--text-muted)'
  }
}

const serverIcon = (name: string): string => {
  const lower = name.toLowerCase()
  if (lower.includes('file')) return 'hard-drive'
  if (lower.includes('git') || lower.includes('github')) return 'code'
  if (lower.includes('database') || lower.includes('db') || lower.includes('sql')) return 'database'
  if (lower.includes('network') || lower.includes('http') || lower.includes('api')) return 'globe'
  return 'server'
}

async function loadServers(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    servers.value = await mcpApi.getServers()
    if (servers.value.length > 0 && !selectedServerId.value) {
      selectedServerId.value = servers.value[0].id
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : '加载服务器列表失败'
  } finally {
    loading.value = false
  }
}

async function loadTools(): Promise<void> {
  if (!selectedServerId.value) {
    tools.value = []
    return
  }
  try {
    tools.value = await mcpApi.getTools(
      selectedServerId.value,
      keyword.value || undefined,
      activeCategory.value !== '全部' ? activeCategory.value : undefined
    )
  } catch {
    tools.value = []
  }
}

function selectServer(serverId: string): void {
  selectedServerId.value = serverId
  keyword.value = ''
  activeCategory.value = '全部'
  testResult.value = null
}

async function handleToggle(tool: McpToolDto): Promise<void> {
  if (toggleLoading.value) return
  toggleLoading.value = tool.id
  try {
    const updated = await mcpApi.toggleTool(tool.id)
    const index = tools.value.findIndex(t => t.id === tool.id)
    if (index !== -1) {
      tools.value[index] = updated
    }
  } catch {
    // Keep current state on failure
  } finally {
    toggleLoading.value = null
  }
}

async function handleTest(toolId: string): Promise<void> {
  testLoading.value = toolId
  testResult.value = null
  try {
    const result = await mcpApi.testTool(toolId)
    testResult.value = { toolId, result }
  } catch (e: unknown) {
    testResult.value = {
      toolId,
      result: {
        success: false,
        message: e instanceof Error ? e.message : '测试失败',
        durationMs: 0
      }
    }
  } finally {
    testLoading.value = null
  }
}

function dismissTestResult(): void {
  testResult.value = null
}

watch(selectedServerId, () => {
  loadTools()
})

onMounted(() => {
  loadServers()
})
</script>

<template>
  <div class="mcp-tools-view">
    <!-- Sidebar -->
    <aside class="server-sidebar">
      <div class="sidebar-header">
        <span class="sidebar-title">MCP 服务器</span>
        <button class="btn-add-server">
          <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
            <path d="M6 1v10M1 6h10" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
          </svg>
          添加服务器
        </button>
      </div>

      <div class="server-list">
        <div v-if="loading" class="sidebar-loading">加载中...</div>
        <div
          v-for="server in servers"
          :key="server.id"
          class="server-item"
          :class="{
            active: selectedServerId === server.id,
            disconnected: server.status === 'disconnected'
          }"
          @click="selectServer(server.id)"
        >
          <!-- Icon -->
          <svg
            v-if="serverIcon(server.name) === 'hard-drive'"
            class="server-icon"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <line x1="22" y1="12" x2="2" y2="12" /><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" /><line x1="6" y1="16" x2="6.01" y2="16" /><line x1="10" y1="16" x2="10.01" y2="16" />
          </svg>
          <svg
            v-else-if="serverIcon(server.name) === 'code'"
            class="server-icon"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <polyline points="16 18 22 12 16 6" /><polyline points="8 6 2 12 8 18" />
          </svg>
          <svg
            v-else-if="serverIcon(server.name) === 'database'"
            class="server-icon"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <ellipse cx="12" cy="5" rx="9" ry="3" /><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3" /><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" />
          </svg>
          <svg
            v-else-if="serverIcon(server.name) === 'globe'"
            class="server-icon"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <circle cx="12" cy="12" r="10" /><line x1="2" y1="12" x2="22" y2="12" /><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
          </svg>
          <svg
            v-else
            class="server-icon"
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <rect
              x="2"
              y="2"
              width="20"
              height="8"
              rx="2"
              ry="2"
            /><rect
              x="2"
              y="14"
              width="20"
              height="8"
              rx="2"
              ry="2"
            /><line x1="6" y1="6" x2="6.01" y2="6" /><line x1="6" y1="18" x2="6.01" y2="18" />
          </svg>

          <div class="server-info">
            <span class="server-name">{{ server.name }}</span>
            <span class="server-tool-count">{{ server.toolCount }} 个工具</span>
          </div>

          <span
            class="status-dot"
            :style="{ background: serverStatusColor(server.status) }"
          />
        </div>

        <div v-if="!loading && servers.length === 0" class="sidebar-empty">
          暂无服务器
        </div>
      </div>
    </aside>

    <!-- Main Content -->
    <main class="main-content-area">
      <!-- Breadcrumb -->
      <nav class="breadcrumb">
        <a href="#/">首页</a>
        <span class="breadcrumb-sep">/</span>
        <a href="#/ai-agent">AI Agent</a>
        <span class="breadcrumb-sep">/</span>
        <span class="breadcrumb-current">MCP 工具管理</span>
      </nav>

      <!-- Page Header -->
      <div class="page-header">
        <div class="header-left">
          <h1 class="page-title">MCP 工具管理</h1>
          <span class="tool-count-badge">{{ tools.length }} 个工具</span>
        </div>
        <div class="search-box">
          <svg
            width="14"
            height="14"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            class="search-icon"
          >
            <circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            v-model="keyword"
            type="text"
            placeholder="搜索工具..."
            class="search-input"
          />
        </div>
      </div>

      <!-- Filter Tabs -->
      <div class="filter-tabs">
        <button
          v-for="cat in categories"
          :key="cat"
          class="filter-tab"
          :class="{ active: activeCategory === cat }"
          @click="activeCategory = cat"
        >
          {{ cat }}
        </button>
      </div>

      <!-- Tool Table -->
      <div class="table-container">
        <!-- Table Header -->
        <div class="table-header">
          <div class="col-name">工具名称</div>
          <div class="col-server">服务器</div>
          <div class="col-desc">描述</div>
          <div class="col-status">状态</div>
          <div class="col-actions">操作</div>
        </div>

        <!-- Loading state -->
        <div v-if="loading" class="table-empty">加载中...</div>

        <!-- Error state -->
        <div v-else-if="error" class="table-error">
          {{ error }}
          <button class="btn-retry" @click="loadServers">重试</button>
        </div>

        <!-- Empty state -->
        <div v-else-if="filteredTools.length === 0" class="table-empty">
          暂无工具数据
        </div>

        <!-- Table Rows -->
        <div
          v-for="(tool, index) in filteredTools"
          :key="tool.id"
          class="table-row"
          :class="{
            'row-even': index % 2 === 1,
            'row-disabled': !tool.isEnabled
          }"
        >
          <div class="col-name">
            <span class="tool-name-text">{{ tool.name }}</span>
          </div>
          <div class="col-server">
            <span class="server-tag">{{ tool.serverName }}</span>
          </div>
          <div class="col-desc">
            <span class="tool-desc-text">{{ tool.description }}</span>
          </div>
          <div class="col-status">
            <ForgeSwitch
              :model-value="tool.isEnabled"
              :disabled="toggleLoading === tool.id"
              @update:model-value="handleToggle(tool)"
            />
          </div>
          <div class="col-actions">
            <button
              class="btn-test"
              :disabled="testLoading === tool.id"
              @click="handleTest(tool.id)"
            >
              {{ testLoading === tool.id ? '测试中...' : '测试' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Test Result Toast -->
      <Teleport to="body">
        <div v-if="testResult" class="test-toast-overlay" @click.self="dismissTestResult">
          <div
            class="test-toast"
            :class="{ 'toast-success': testResult.result.success, 'toast-fail': !testResult.result.success }"
          >
            <div class="toast-header">
              <span class="toast-icon">{{ testResult.result.success ? '✓' : '✗' }}</span>
              <span class="toast-title">{{ testResult.result.success ? '测试通过' : '测试失败' }}</span>
              <button class="toast-close" @click="dismissTestResult">×</button>
            </div>
            <div class="toast-body">
              <p>{{ testResult.result.message }}</p>
              <p v-if="testResult.result.durationMs > 0" class="toast-duration">
                耗时: {{ testResult.result.durationMs }}ms
              </p>
            </div>
          </div>
        </div>
      </Teleport>
    </main>
  </div>
</template>

<style scoped>
.mcp-tools-view {
  display: flex;
  height: 100%;
  overflow: hidden;
}

/* ── Sidebar ── */
.server-sidebar {
  width: 220px;
  min-width: 220px;
  display: flex;
  flex-direction: column;
  padding: var(--space-4);
  border-right: 1px solid var(--border-color);
  background: var(--bg-secondary);
  overflow-y: auto;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--space-4);
}

.sidebar-title {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--text-primary);
}

.btn-add-server {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--primary-color);
  border: 1px solid var(--primary-color);
  border-radius: var(--radius-sm);
  background: transparent;
  cursor: pointer;
  transition: background var(--motion-fast);
}

.btn-add-server:hover {
  background: var(--primary-light);
}

.server-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.server-item {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-3);
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: background var(--motion-fast), border-color var(--motion-fast);
  border: 1px solid transparent;
}

.server-item:hover {
  background: var(--bg-hover);
}

.server-item.active {
  background: var(--primary-soft);
  border-color: var(--primary-color);
}

.server-item.disconnected {
  opacity: 0.5;
}

.server-icon {
  flex-shrink: 0;
  color: var(--primary-color);
}

.disconnected .server-icon {
  color: var(--text-muted);
}

.server-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.server-name {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.server-tool-count {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.sidebar-loading,
.sidebar-empty {
  padding: var(--space-4);
  text-align: center;
  font-size: 0.8125rem;
  color: var(--text-muted);
}

/* ── Main Content ── */
.main-content-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: auto;
  background: var(--bg-primary);
}

/* Breadcrumb */
.breadcrumb {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-4) var(--space-6) var(--space-2);
}

.breadcrumb a {
  font-size: 0.75rem;
  color: var(--text-muted);
  text-decoration: none;
  transition: color var(--motion-fast);
}

.breadcrumb a:hover {
  color: var(--primary-color);
}

.breadcrumb-sep {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.breadcrumb-current {
  font-size: 0.75rem;
  color: var(--text-secondary);
  font-weight: 500;
}

/* Page Header */
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--space-4) var(--space-6);
  border-bottom: 1px solid var(--border-color);
}

.header-left {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.page-title {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.tool-count-badge {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--primary-color);
  background: var(--primary-light);
  border: 1px solid var(--primary-color);
  border-radius: var(--radius-pill);
  padding: 2px 10px;
}

.search-box {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: 6px 12px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  background: var(--bg-tertiary);
  min-width: 220px;
}

.search-icon {
  flex-shrink: 0;
  color: var(--text-muted);
}

.search-input {
  border: none;
  background: transparent;
  outline: none;
  font-size: 0.8125rem;
  color: var(--text-primary);
  width: 100%;
}

.search-input::placeholder {
  color: var(--text-muted);
}

/* Filter Tabs */
.filter-tabs {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-6);
}

.filter-tab {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--text-secondary);
  padding: var(--space-2) var(--space-4);
  border-radius: var(--radius-md);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all var(--motion-fast);
  background: transparent;
}

.filter-tab:hover {
  color: var(--text-primary);
  background: var(--bg-hover);
}

.filter-tab.active {
  color: var(--primary-color);
  background: var(--primary-light);
  border-color: var(--primary-color);
}

/* Table */
.table-container {
  flex: 1;
  margin: 0 var(--space-6) var(--space-6);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  overflow: hidden;
}

.table-header {
  display: flex;
  align-items: center;
  padding: 0 var(--space-4);
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
  min-height: 40px;
}

.table-header > div {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.col-name {
  width: 180px;
  flex-shrink: 0;
}

.col-server {
  width: 130px;
  flex-shrink: 0;
}

.col-desc {
  flex: 1;
}

.col-status {
  width: 70px;
  flex-shrink: 0;
  text-align: center;
}

.col-actions {
  width: 100px;
  flex-shrink: 0;
  text-align: right;
}

/* Table Rows */
.table-row {
  display: flex;
  align-items: center;
  padding: 0 var(--space-4);
  min-height: 48px;
  border-bottom: 1px solid var(--border-light);
  transition: background var(--motion-fast);
}

.table-row:last-child {
  border-bottom: none;
}

.table-row:hover {
  background: var(--bg-hover);
}

.table-row.row-even {
  background: rgba(245, 158, 11, 0.04);
}

.table-row.row-even:hover {
  background: var(--bg-hover);
}

.table-row.row-disabled .tool-name-text,
.table-row.row-disabled .tool-desc-text {
  color: var(--text-muted);
}

.tool-name-text {
  font-family: var(--font-family-mono);
  font-size: 0.8125rem;
  color: var(--text-primary);
}

.server-tag {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--primary-color);
  background: var(--primary-light);
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  display: inline-block;
}

.row-disabled .server-tag {
  color: var(--text-muted);
  background: var(--bg-muted);
}

.tool-desc-text {
  font-size: 0.8125rem;
  color: var(--text-secondary);
}

.row-disabled .tool-desc-text {
  color: var(--text-muted);
}

.btn-test {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-sm);
  background: transparent;
  padding: 2px 10px;
  cursor: pointer;
  transition: all var(--motion-fast);
}

.btn-test:hover:not(:disabled) {
  color: var(--text-primary);
  border-color: var(--border-strong);
}

.btn-test:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.row-disabled .btn-test {
  color: var(--text-muted);
  border-color: var(--border-light);
}

/* Empty / Error states */
.table-empty,
.table-error {
  padding: var(--space-10);
  text-align: center;
  font-size: 0.875rem;
  color: var(--text-muted);
}

.table-error {
  color: var(--danger-color);
}

.btn-retry {
  margin-left: var(--space-2);
  font-size: 0.75rem;
  color: var(--primary-color);
  background: transparent;
  border: 1px solid var(--primary-color);
  border-radius: var(--radius-sm);
  padding: 2px 10px;
  cursor: pointer;
}

/* Test Result Toast */
.test-toast-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.3);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.test-toast {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  padding: var(--space-5);
  min-width: 320px;
  max-width: 480px;
  box-shadow: var(--shadow-lg);
}

.toast-header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin-bottom: var(--space-3);
}

.toast-icon {
  font-size: 1.25rem;
  font-weight: 700;
}

.toast-success .toast-icon {
  color: var(--success-color);
}

.toast-fail .toast-icon {
  color: var(--danger-color);
}

.toast-title {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--text-primary);
  flex: 1;
}

.toast-close {
  font-size: 1.25rem;
  color: var(--text-muted);
  background: none;
  border: none;
  cursor: pointer;
  line-height: 1;
  padding: 0;
}

.toast-body {
  font-size: 0.8125rem;
  color: var(--text-secondary);
}

.toast-body p {
  margin: 0;
}

.toast-duration {
  margin-top: var(--space-2) !important;
  color: var(--text-muted);
  font-size: 0.75rem;
}
</style>
