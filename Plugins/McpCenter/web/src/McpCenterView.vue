<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
// 界面组件显式 import（契约：经宿主 import map → public/shared/element-plus.js shim →
// window.__FORGE_SHARED__.elementPlus 取宿主同一份实例；宿主 exposeSharedDeps 已暴露这些组件）。
// 模板 `<ElXxx>` 若未显式导入会编译成 resolveComponent（宿主全局注册表无按需组件）→ 组件静默失效。
import { ElMessage, ElTabs, ElTabPane, ElSwitch, ElInputNumber, ElInput, ElCheckbox, ElButton, ElSelect, ElOption, ElDialog, ElTag } from 'element-plus'
import { fetchMcpServers, fetchMcpTools, testMcpServer, testMcpTool, toggleMcpTool } from './api/mcp'
import { fetchGatewayConfig, updateGatewayConfig } from './api/gateway'
import { fetchPluginVersion } from './http'
import {
  fetchExternalServers,
  createExternalServer,
  updateExternalServer,
  deleteExternalServer,
  connectExternalServer,
  disconnectExternalServer,
  fetchExternalTools,
  testExternalServer,
} from './api/external'
import type { McpServerDto, McpTestResultDto, McpToolDto } from './types/mcp'
import type { McpCenterConfigDto } from './types/gateway'
import type { McpExternalServerStateDto, McpExternalServerUpsertDto, McpExternalToolDto } from './types/external'

/* ── 版本徽标（铁律 13：GET /api/plugin 解包 .data 按 id 过滤） ── */
const version = ref('')
const activeTab = ref('tools')

/* ── 工具管理（迁自宿主 McpToolsView 全功能） ── */
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
    case 'connected': return 'var(--brand-amber-500, #f59e0b)'
    case 'disconnected': return 'var(--el-text-color-secondary)'
    case 'error': return 'var(--el-color-danger)'
    default: return 'var(--el-text-color-secondary)'
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
    servers.value = (await fetchMcpServers()) ?? []
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
    tools.value = (await fetchMcpTools(
      selectedServerId.value,
      keyword.value || undefined,
      activeCategory.value !== '全部' ? activeCategory.value : undefined
    )) ?? []
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
    const updated = await toggleMcpTool(tool.id)
    const index = tools.value.findIndex(t => t.id === tool.id)
    if (updated && index !== -1) {
      tools.value[index] = updated
    }
  } catch {
    // 保持原状态
  } finally {
    toggleLoading.value = null
  }
}

async function handleTest(toolId: string): Promise<void> {
  testLoading.value = toolId
  testResult.value = null
  try {
    const result = await testMcpTool(toolId)
    testResult.value = { toolId, result: result ?? { success: false, message: '无响应', durationMs: 0 } }
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

/* ── 网关配置（v2.0.0 新增） ── */
const gatewayConfig = ref<McpCenterConfigDto | null>(null)
const configLoading = ref(false)
const savingConfig = ref(false)
const portInput = ref<number>(18890)
const listenHostInput = ref('127.0.0.1')
const tokenInput = ref('')
const clearTokenFlag = ref(false)
const configCopyHint = ref(false)

async function loadGatewayConfig(): Promise<void> {
  configLoading.value = true
  try {
    const cfg = await fetchGatewayConfig()
    if (cfg) {
      gatewayConfig.value = cfg
      portInput.value = cfg.port
      listenHostInput.value = cfg.listenHost
      tokenInput.value = ''
      clearTokenFlag.value = false
    }
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载网关配置失败')
  } finally {
    configLoading.value = false
  }
}

async function copyListenUrl(): Promise<void> {
  const url = gatewayConfig.value?.listenUrl
  if (!url) return
  try {
    await navigator.clipboard.writeText(url)
    configCopyHint.value = true
    setTimeout(() => { configCopyHint.value = false }, 2000)
  } catch {
    ElMessage.warning('复制失败，请手动复制地址')
  }
}

async function saveGatewayConfig(): Promise<void> {
  savingConfig.value = true
  try {
    const update: { port?: number; listenHost?: string; token?: string } = {}
    if (portInput.value !== gatewayConfig.value?.port) {
      update.port = portInput.value
    }
    if (listenHostInput.value.trim() !== gatewayConfig.value?.listenHost) {
      update.listenHost = listenHostInput.value.trim()
    }
    if (tokenInput.value) {
      update.token = tokenInput.value.trim()
    } else if (clearTokenFlag.value) {
      update.token = '' // 显式清除令牌
    }
    // 三个字段都未变化时不发请求
    if (Object.keys(update).length === 0) {
      ElMessage.info('没有需要保存的修改')
      return
    }
    const cfg = await updateGatewayConfig(update)
    if (cfg) {
      gatewayConfig.value = cfg
      portInput.value = cfg.port
      listenHostInput.value = cfg.listenHost
      tokenInput.value = ''
      clearTokenFlag.value = false
      ElMessage.success('网关配置已保存并热重启生效')
    }
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '保存失败')
  } finally {
    savingConfig.value = false
  }
}

watch(selectedServerId, () => {
  loadTools()
})

/* ── 外部服务器管理（v2.1.0：标准 MCP 客户端接入） ── */
const externalServers = ref<McpExternalServerStateDto[]>([])
const externalLoading = ref(false)
const externalToolsLoading = ref(false)
const externalToolsOf = ref<string | null>(null)
const externalTools = ref<McpExternalToolDto[]>([])
const externalToolsDialog = ref(false)
const externalBusy = ref<string | null>(null) // 操作中的服务器 id

// 新增/编辑表单
const externalFormVisible = ref(false)
const externalFormTitle = ref('')
interface ExternalFormModel {
  id: string
  name: string
  enabled: boolean
  transport: 'stdio' | 'streamable-http' | 'http-sse'
  url: string
  headersText: string
  command: string
  argsText: string
  envText: string
}
const externalForm = ref<ExternalFormModel>({
  id: '',
  name: '',
  enabled: true,
  transport: 'stdio',
  url: '',
  headersText: '',
  command: 'npx',
  argsText: '',
  envText: '',
})
const externalFormSaving = ref(false)
const externalEditingId = ref<string | null>(null)

async function loadExternalServers(): Promise<void> {
  externalLoading.value = true
  try {
    externalServers.value = (await fetchExternalServers()) ?? []
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载外部服务器失败')
  } finally {
    externalLoading.value = false
  }
}

const transportLabel = (t: string): string => {
  switch (t) {
    case 'stdio': return 'stdio'
    case 'streamable-http': return 'Streamable HTTP'
    case 'http-sse': return 'HTTP + SSE'
    default: return t
  }
}

const externalStatusText = (s: McpExternalServerStateDto): string => {
  if (!s.enabled) return '已停用'
  if (s.connected) return '已连接'
  if (s.lastError) return '连接失败'
  return '未连接'
}

const externalStatusType = (s: McpExternalServerStateDto): 'success' | 'info' | 'danger' | 'warning' => {
  if (!s.enabled) return 'info'
  if (s.connected) return 'success'
  if (s.lastError) return 'danger'
  return 'warning'
}

function openCreateExternal(): void {
  externalEditingId.value = null
  externalFormTitle.value = '新增外部服务器'
  externalForm.value = {
    id: '',
    name: '',
    enabled: true,
    transport: 'stdio',
    url: '',
    headersText: '',
    command: 'npx',
    argsText: '',
    envText: '',
  }
  externalFormVisible.value = true
}

function openEditExternal(s: McpExternalServerStateDto): void {
  externalEditingId.value = s.id
  externalFormTitle.value = `编辑 ${s.name}`
  externalForm.value = {
    id: s.id,
    name: s.name,
    enabled: s.enabled,
    transport: s.transport as 'stdio' | 'streamable-http' | 'http-sse',
    url: '',
    headersText: kvToText(s.headersMasked),
    command: '',
    argsText: '',
    envText: kvToText(s.envMasked),
  }
  externalFormVisible.value = true
}

function kvToText(kv: Record<string, string> | undefined): string {
  if (!kv) return ''
  return Object.entries(kv)
    .map(([k, v]) => `${k}: ${v}`)
    .join('\n')
}

function parseKv(text: string | undefined): Record<string, string> {
  const out: Record<string, string> = {}
  if (!text) return out
  for (const line of text.split('\n')) {
    const t = line.trim()
    if (!t) continue
    const idx = t.indexOf(':')
    if (idx <= 0) continue
    out[t.slice(0, idx).trim()] = t.slice(idx + 1).trim()
  }
  return out
}

function parseArgs(text: string | undefined): string[] {
  if (!text) return []
  // 简单分词：按空白拆分（不支持引号内空格——引号场景请用 JSON 数组）
  return text.split(/\s+/).filter(Boolean)
}

async function saveExternal(): Promise<void> {
  const f = externalForm.value
  if (!f.id.trim() || !f.name.trim()) {
    ElMessage.warning('请填写 id 与名称')
    return
  }
  if (f.transport !== 'stdio' && !f.url?.trim()) {
    ElMessage.warning('HTTP 传输必须填写 URL')
    return
  }
  externalFormSaving.value = true
  try {
    const body: McpExternalServerUpsertDto = {
      id: f.id.trim(),
      name: f.name.trim(),
      enabled: f.enabled,
      transport: f.transport,
      url: f.transport === 'stdio' ? undefined : f.url?.trim(),
      headers: parseKv(f.headersText),
      command: f.transport === 'stdio' ? f.command?.trim() || 'npx' : undefined,
      args: f.transport === 'stdio' ? parseArgs(f.argsText) : undefined,
      env: parseKv(f.envText),
    }
    if (externalEditingId.value) {
      await updateExternalServer(externalEditingId.value, body)
      ElMessage.success('外部服务器已更新')
    } else {
      await createExternalServer(body)
      ElMessage.success('外部服务器已添加')
    }
    externalFormVisible.value = false
    await loadExternalServers()
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '保存失败')
  } finally {
    externalFormSaving.value = false
  }
}

async function handleDeleteExternal(s: McpExternalServerStateDto): Promise<void> {
  if (!window.confirm(`确认删除外部服务器「${s.name}」？其配置将从 external-servers.json 移除。`)) return
  externalBusy.value = s.id
  try {
    await deleteExternalServer(s.id)
    ElMessage.success('已删除')
    await loadExternalServers()
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '删除失败')
  } finally {
    externalBusy.value = null
  }
}

async function handleConnectExternal(s: McpExternalServerStateDto): Promise<void> {
  externalBusy.value = s.id
  try {
    const state = await connectExternalServer(s.id)
    if (state?.connected) ElMessage.success(`${s.name} 已连接`)
    else ElMessage.warning(state?.lastError ? `连接失败: ${state.lastError}` : '连接未建立')
    await loadExternalServers()
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '连接失败')
  } finally {
    externalBusy.value = null
  }
}

async function handleDisconnectExternal(s: McpExternalServerStateDto): Promise<void> {
  externalBusy.value = s.id
  try {
    await disconnectExternalServer(s.id)
    ElMessage.success(`${s.name} 已断开`)
    await loadExternalServers()
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '断开失败')
  } finally {
    externalBusy.value = null
  }
}

async function handleTestExternal(s: McpExternalServerStateDto): Promise<void> {
  externalBusy.value = s.id
  try {
    const r = await testExternalServer(s.id)
    if (r?.success) ElMessage.success(`${s.name} 握手成功（${r.durationMs}ms${s.protocolVersion ? `，协议 ${s.protocolVersion}` : ''}）`)
    else ElMessage.error(r?.message || '测试失败')
    await loadExternalServers()
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '测试失败')
  } finally {
    externalBusy.value = null
  }
}

async function handleShowExternalTools(s: McpExternalServerStateDto): Promise<void> {
  if (externalToolsLoading.value) return
  externalToolsOf.value = s.id
  externalToolsDialog.value = true
  externalTools.value = []
  externalToolsLoading.value = true
  try {
    externalTools.value = (await fetchExternalTools(s.id)) ?? []
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '拉取工具失败')
  } finally {
    externalToolsLoading.value = false
  }
}

onMounted(() => {
  loadServers()
  loadGatewayConfig()
  loadExternalServers()
  fetchPluginVersion('mcp-center').then(v => { version.value = v })
})
</script>

<template>
  <div class="mcp-center-view">
    <!-- ── 侧栏：外部 MCP 服务器 ── -->
    <aside class="server-sidebar">
      <div class="sidebar-header">
        <span class="sidebar-title">MCP 服务器</span>
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
          <svg
            v-if="serverIcon(server.name) === 'hard-drive'"
            class="server-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          >
            <line x1="22" y1="12" x2="2" y2="12" /><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" /><line x1="6" y1="16" x2="6.01" y2="16" /><line x1="10" y1="16" x2="10.01" y2="16" />
          </svg>
          <svg
            v-else-if="serverIcon(server.name) === 'code'"
            class="server-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          >
            <polyline points="16 18 22 12 16 6" /><polyline points="8 6 2 12 8 18" />
          </svg>
          <svg
            v-else-if="serverIcon(server.name) === 'database'"
            class="server-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          >
            <ellipse cx="12" cy="5" rx="9" ry="3" /><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3" /><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" />
          </svg>
          <svg
            v-else-if="serverIcon(server.name) === 'globe'"
            class="server-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          >
            <circle cx="12" cy="12" r="10" /><line x1="2" y1="12" x2="22" y2="12" /><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
          </svg>
          <svg
            v-else
            class="server-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
          >
            <rect x="2" y="2" width="20" height="8" rx="2" ry="2" /><rect x="2" y="14" width="20" height="8" rx="2" ry="2" /><line x1="6" y1="6" x2="6.01" y2="6" /><line x1="6" y1="18" x2="6.01" y2="18" />
          </svg>

          <div class="server-info">
            <span class="server-name">{{ server.name }}</span>
            <span class="server-tool-count">{{ server.toolCount }} 个工具</span>
          </div>

          <span class="status-dot" :style="{ background: serverStatusColor(server.status) }" />
        </div>

        <div v-if="!loading && servers.length === 0" class="sidebar-empty">
          暂无服务器
        </div>
      </div>
    </aside>

    <!-- ── 主内容区 ── -->
    <main class="main-content-area">
      <div class="page-header">
        <div class="header-left">
          <h1 class="page-title">MCP 中心</h1>
          <span v-if="version" class="version-badge">v{{ version }}</span>
          <span v-if="gatewayConfig" class="gateway-address-chip" :title="gatewayConfig.listenUrl">
            {{ gatewayConfig.listenUrl }}
          </span>
        </div>
        <div class="header-right">
          <span
            class="server-state"
            :class="{ running: gatewayConfig?.isRunning }"
          >
            <span class="state-dot" />
            {{ gatewayConfig?.isRunning ? 'MCP 服务运行中' : 'MCP 服务未运行' }}
          </span>
        </div>
      </div>

      <div class="tab-area">
        <ElTabs v-model="activeTab" class="mcp-center-tabs">
          <!-- ═══ 工具管理 ═══ -->
          <ElTabPane label="工具管理" name="tools">
            <div class="tool-panel">
              <div class="filter-row">
                <div class="search-box">
                  <svg
                    width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                    stroke-linecap="round" stroke-linejoin="round" class="search-icon"
                  >
                    <circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
                  </svg>
                  <input
                    v-model="keyword" type="text" placeholder="搜索工具..." class="search-input"
                  />
                </div>
                <div class="filter-tabs">
                  <button
                    v-for="cat in categories" :key="cat" class="filter-tab"
                    :class="{ active: activeCategory === cat }" @click="activeCategory = cat"
                  >
                    {{ cat }}
                  </button>
                </div>
              </div>

              <div class="table-container">
                <div class="table-header">
                  <div class="col-name">工具名称</div>
                  <div class="col-server">服务器</div>
                  <div class="col-desc">描述</div>
                  <div class="col-status">状态</div>
                  <div class="col-actions">操作</div>
                </div>

                <div v-if="loading" class="table-empty">加载中...</div>
                <div v-else-if="error" class="table-error">
                  {{ error }}
                  <button class="btn-retry" @click="loadServers">重试</button>
                </div>
                <div v-else-if="filteredTools.length === 0" class="table-empty">
                  暂无工具数据
                </div>

                <div
                  v-for="(tool, index) in filteredTools" :key="tool.id"
                  class="table-row"
                  :class="{ 'row-even': index % 2 === 1, 'row-disabled': !tool.isEnabled }"
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
                    <ElSwitch
                      :model-value="tool.isEnabled"
                      :disabled="toggleLoading === tool.id"
                      @change="handleToggle(tool)"
                    />
                  </div>
                  <div class="col-actions">
                    <button
                      class="btn-test" :disabled="testLoading === tool.id"
                      @click="handleTest(tool.id)"
                    >
                      {{ testLoading === tool.id ? '测试中...' : '测试' }}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </ElTabPane>

          <!-- ═══ 网关配置 ═══ -->
          <ElTabPane label="网关配置" name="gateway">
            <div class="gateway-panel">
              <div v-loading="configLoading" class="gateway-status-cards">
                <div class="status-card">
                  <div class="card-label">MCP 服务地址</div>
                  <div class="card-value mono">
                    {{ gatewayConfig?.listenUrl ?? '加载中...' }}
                  </div>
                  <button
                    class="btn-copy" :disabled="!gatewayConfig"
                    @click="copyListenUrl"
                  >
                    {{ configCopyHint ? '已复制 ✓' : '复制地址' }}
                  </button>
                </div>
                <div class="status-card">
                  <div class="card-label">运行状态</div>
                  <div class="card-value">
                    <span
                      class="state-text"
                      :class="{ 'state-on': gatewayConfig?.isRunning, 'state-off': !gatewayConfig?.isRunning }"
                    >
                      {{ gatewayConfig?.isRunning ? '正在监听' : '未运行' }}
                    </span>
                  </div>
                  <div class="card-sub">
                    监听 {{ gatewayConfig?.listenHost ?? '-' }}:{{ gatewayConfig?.port ?? '-' }}
                  </div>
                </div>
                <div class="status-card">
                  <div class="card-label">访问令牌</div>
                  <div class="card-value">
                    <span v-if="gatewayConfig?.hasToken" class="token-masked">{{ gatewayConfig.tokenMasked }}</span>
                    <span v-else class="token-none">未设置（无鉴权）</span>
                  </div>
                  <div class="card-sub">客户端需带 Authorization: Bearer &lt;token&gt;</div>
                </div>
              </div>

              <div class="gateway-form">
                <h2 class="form-title">修改网关配置</h2>
                <p class="form-desc">保存后自动重启内置 MCP 服务，新配置立即生效；失败自动回滚。</p>

                <div class="form-row">
                  <label class="form-label">监听端口</label>
                  <ElInputNumber
                    v-model="portInput" :min="1024" :max="65535" :step="1" controls-position="right"
                    class="port-input"
                  />
                  <span class="form-hint">范围 1024-65535</span>
                </div>

                <div class="form-row">
                  <label class="form-label">监听地址</label>
                  <ElInput v-model="listenHostInput" placeholder="127.0.0.1 / 0.0.0.0" class="host-input" />
                  <span class="form-hint">0.0.0.0 表示局域网可访问</span>
                </div>

                <div class="form-row">
                  <label class="form-label">访问令牌</label>
                  <ElInput
                    v-model="tokenInput" type="password" show-password
                    :placeholder="gatewayConfig?.hasToken ? '留空保持不变（输入新值覆盖）' : '输入令牌启用鉴权'"
                    class="token-input"
                  />
                </div>

                <div v-if="gatewayConfig?.hasToken" class="form-row row-clear-token">
                  <ElCheckbox v-model="clearTokenFlag">
                    清除当前令牌（使网关恢复无鉴权）
                  </ElCheckbox>
                </div>

                <div class="form-actions">
                  <ElButton type="primary" :loading="savingConfig" @click="saveGatewayConfig">
                    保存并重启生效
                  </ElButton>
                </div>
              </div>

              <!-- ═══ 外部服务器（v2.1.0 标准 MCP 客户端接入） ═══ -->
              <div class="external-section">
                <div class="external-head">
                  <div class="external-title-area">
                    <h2 class="form-title">外部 MCP 服务器</h2>
                    <p class="form-desc">
                      按标准 MCP 协议连接外部服务器（stdio / Streamable HTTP / HTTP+SSE），
                      其工具经 <code class="mono-inline">universal_tool</code> 的
                      <code class="mono-inline">mcp.&lt;服务器id&gt;.&lt;工具名&gt;</code> 命名空间统一转发。
                    </p>
                  </div>
                  <ElButton type="primary" size="small" @click="openCreateExternal">新增服务器</ElButton>
                </div>

                <div v-loading="externalLoading" class="external-list">
                  <div v-if="!externalLoading && externalServers.length === 0" class="external-empty">
                    尚未配置外部服务器
                  </div>
                  <div
                    v-for="s in externalServers" :key="s.id"
                    class="external-item"
                    :class="{ 'item-off': !s.enabled }"
                  >
                    <div class="external-item-main">
                      <div class="external-item-name">
                        <span class="external-name-text">{{ s.name }}</span>
                        <ElTag size="small" :type="externalStatusType(s)" effect="light">
                          {{ externalStatusText(s) }}
                        </ElTag>
                        <ElTag size="small" type="info" effect="plain">{{ transportLabel(s.transport) }}</ElTag>
                      </div>
                      <div class="external-item-sub">
                        <code class="mono-inline">mcp.{{ s.id }}.&lt;工具名&gt;</code>
                        <span v-if="s.toolCount > 0" class="external-toolcount">{{ s.toolCount }} 个工具</span>
                        <span v-if="s.protocolVersion" class="external-toolcount">协议 {{ s.protocolVersion }}</span>
                        <span v-if="s.serverInfoName" class="external-toolcount">{{ s.serverInfoName }}</span>
                        <span v-if="s.lastError" class="external-error-text" :title="s.lastError">{{ s.lastError }}</span>
                      </div>
                    </div>
                    <div class="external-item-actions">
                      <ElButton
                        v-if="s.enabled && !s.connected" size="small" :loading="externalBusy === s.id"
                        @click="handleConnectExternal(s)"
                      >连接</ElButton>
                      <ElButton
                        v-if="s.enabled && s.connected" size="small" :loading="externalBusy === s.id"
                        @click="handleDisconnectExternal(s)"
                      >断开</ElButton>
                      <ElButton size="small" :loading="externalBusy === s.id" @click="handleTestExternal(s)">测试</ElButton>
                      <ElButton size="small" @click="handleShowExternalTools(s)">工具</ElButton>
                      <ElButton size="small" @click="openEditExternal(s)">编辑</ElButton>
                      <ElButton size="small" type="danger" plain :loading="externalBusy === s.id" @click="handleDeleteExternal(s)">删除</ElButton>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </ElTabPane>
        </ElTabs>
      </div>

      <!-- 工具测试结果 Toast -->
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

      <!-- 新增/编辑外部服务器对话框 -->
      <ElDialog
        :model-value="externalFormVisible"
        :title="externalFormTitle"
        width="560px"
        append-to-body
        @update:model-value="v => { externalFormVisible = v }"
      >
        <div class="external-form-body">
          <div class="external-form-row">
            <label class="external-form-label">ID</label>
            <ElInput
              v-model="externalForm.id" :disabled="!!externalEditingId"
              placeholder="kebab-case，如 deepwiki" class="external-form-control"
            />
          </div>
          <div class="external-form-row">
            <label class="external-form-label">名称</label>
            <ElInput v-model="externalForm.name" placeholder="显示名，如 DeepWiki" class="external-form-control" />
          </div>
          <div class="external-form-row">
            <label class="external-form-label">传输</label>
            <ElSelect v-model="externalForm.transport" class="external-form-control">
              <ElOption label="stdio（本地命令）" value="stdio" />
              <ElOption label="Streamable HTTP（MCP 2.0）" value="streamable-http" />
              <ElOption label="HTTP + SSE（旧版）" value="http-sse" />
            </ElSelect>
          </div>

          <template v-if="externalForm.transport !== 'stdio'">
            <div class="external-form-row">
              <label class="external-form-label">URL</label>
              <ElInput
                v-model="externalForm.url"
                placeholder="http://127.0.0.1:3800/mcp"
                class="external-form-control"
              />
            </div>
            <div class="external-form-row">
              <label class="external-form-label">请求头</label>
              <ElInput
                v-model="externalForm.headersText"
                type="textarea" :rows="2"
                placeholder="Authorization: Bearer xxx&#10;每行一个：键: 值"
                class="external-form-control"
              />
            </div>
          </template>

          <template v-else>
            <div class="external-form-row">
              <label class="external-form-label">命令</label>
              <ElSelect v-model="externalForm.command" class="external-form-control" allow-create filterable>
                <ElOption label="npx" value="npx" />
                <ElOption label="node" value="node" />
                <ElOption label="python" value="python" />
                <ElOption label="uvx" value="uvx" />
                <ElOption label="dotnet" value="dotnet" />
              </ElSelect>
            </div>
            <div class="external-form-row">
              <label class="external-form-label">参数</label>
              <ElInput
                v-model="externalForm.argsText"
                placeholder="npx 包名或脚本路径（空格分隔）"
                class="external-form-control"
              />
            </div>
            <div class="external-form-row">
              <label class="external-form-label">环境变量</label>
              <ElInput
                v-model="externalForm.envText"
                type="textarea" :rows="2"
                placeholder="API_KEY: xxx&#10;每行一个：键: 值"
                class="external-form-control"
              />
            </div>
          </template>

          <div class="external-form-row">
            <ElCheckbox v-model="externalForm.enabled">保存后自动连接</ElCheckbox>
          </div>
          <p class="external-form-help">
            对外统一工具名：<code class="mono-inline">mcp.{{ externalForm.id || '&lt;服务器id&gt;' }}.&lt;工具名&gt;</code>
          </p>
        </div>
        <template #footer>
          <ElButton @click="externalFormVisible = false">取消</ElButton>
          <ElButton type="primary" :loading="externalFormSaving" @click="saveExternal">保存</ElButton>
        </template>
      </ElDialog>

      <!-- 外部工具清单对话框 -->
      <ElDialog
        :model-value="externalToolsDialog"
        title="外部服务器工具"
        width="560px"
        append-to-body
        class="external-tools-dialog"
        @update:model-value="v => { externalToolsDialog = v }"
      >
        <div v-loading="externalToolsLoading">
          <div v-if="!externalToolsLoading && externalTools.length === 0" class="external-tools-empty">
            该服务器未暴露工具（或未连接）
          </div>
          <div v-for="t in externalTools" :key="t.fullName" class="external-tool-item">
            <span class="external-tool-name">{{ t.fullName }}</span>
            <span class="external-tool-desc">{{ t.description || '（无描述）' }}</span>
          </div>
        </div>
      </ElDialog>
    </main>
  </div>
</template>

<script lang="ts">
export default { name: 'McpCenterView' }
</script>

<style scoped>
.mcp-center-view {
  display: flex;
  height: 100%;
  overflow: hidden;
}

/* ── 侧栏 ── */
.server-sidebar {
  width: 220px;
  min-width: 220px;
  display: flex;
  flex-direction: column;
  padding: 16px;
  border-right: 1px solid var(--el-border-color);
  background: var(--el-bg-color-page);
  overflow-y: auto;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.sidebar-title {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.server-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.server-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border-radius: var(--el-border-radius-base);
  cursor: pointer;
  transition: background 150ms ease, border-color 150ms ease;
  border: 1px solid transparent;
}

.server-item:hover { background: var(--el-fill-color); }

.server-item.active {
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary);
}

.server-item.disconnected { opacity: 0.5; }

.server-icon {
  flex-shrink: 0;
  color: var(--el-color-primary);
}

.disconnected .server-icon { color: var(--el-text-color-secondary); }

.server-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.server-name {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--el-text-color-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.server-tool-count {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.sidebar-loading,
.sidebar-empty {
  padding: 16px;
  text-align: center;
  font-size: 0.8125rem;
  color: var(--el-text-color-secondary);
}

/* ── 主内容区 ── */
.main-content-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--el-bg-color);
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 24px;
  border-bottom: 1px solid var(--el-border-color);
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.page-title {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--el-text-color-primary);
  margin: 0;
}

.version-badge {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border-radius: 999px;
  padding: 2px 10px;
  flex-shrink: 0;
}

.gateway-address-chip {
  font-family: var(--font-family-mono, ui-monospace, monospace);
  font-size: 0.75rem;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border: 1px solid var(--el-color-primary-light-5);
  border-radius: var(--el-border-radius-small);
  padding: 2px 10px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 260px;
}

.header-right {
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.server-state {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.state-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--el-text-color-secondary);
}

.server-state.running .state-dot {
  background: var(--el-color-success);
}

.server-state.running {
  color: var(--el-color-success);
}

/* ── Tabs ── */
.tab-area {
  flex: 1;
  overflow: auto;
  padding: 0 24px;
}

.mcp-center-tabs :deep(.el-tabs__header) {
  margin-bottom: 12px;
}

/* ── 工具管理 ── */
.tool-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding-bottom: 24px;
}

.filter-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}

.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  background: var(--el-fill-color-light);
  min-width: 220px;
}

.search-icon {
  flex-shrink: 0;
  color: var(--el-text-color-secondary);
}

.search-input {
  border: none;
  background: transparent;
  outline: none;
  font-size: 0.8125rem;
  color: var(--el-text-color-primary);
  width: 100%;
}

.search-input::placeholder { color: var(--el-text-color-secondary); }

.filter-tabs {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.filter-tab {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--el-text-color-regular);
  padding: 6px 14px;
  border-radius: var(--el-border-radius-base);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all 150ms ease;
  background: transparent;
}

.filter-tab:hover {
  color: var(--el-text-color-primary);
  background: var(--el-fill-color);
}

.filter-tab.active {
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary);
}

.table-container {
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  overflow: hidden;
}

.table-header {
  display: flex;
  align-items: center;
  padding: 0 16px;
  background: var(--el-bg-color-page);
  border-bottom: 1px solid var(--el-border-color);
  min-height: 40px;
}

.table-header > div {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.col-name { width: 180px; flex-shrink: 0; }
.col-server { width: 130px; flex-shrink: 0; }
.col-desc { flex: 1; }
.col-status { width: 70px; flex-shrink: 0; text-align: center; }
.col-actions { width: 100px; flex-shrink: 0; text-align: right; }

.table-row {
  display: flex;
  align-items: center;
  padding: 0 16px;
  min-height: 48px;
  border-bottom: 1px solid var(--el-border-color-light);
  transition: background 150ms ease;
}

.table-row:last-child { border-bottom: none; }
.table-row:hover { background: var(--el-fill-color); }
.table-row.row-even { background: rgba(245, 158, 11, 0.04); }
.table-row.row-even:hover { background: var(--el-fill-color); }

.table-row.row-disabled .tool-name-text,
.table-row.row-disabled .tool-desc-text {
  color: var(--el-text-color-secondary);
}

.tool-name-text {
  font-family: var(--font-family-mono, ui-monospace, monospace);
  font-size: 0.8125rem;
  color: var(--el-text-color-primary);
}

.server-tag {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  padding: 2px 8px;
  border-radius: var(--el-border-radius-small);
  display: inline-block;
}

.row-disabled .server-tag {
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
}

.tool-desc-text {
  font-size: 0.8125rem;
  color: var(--el-text-color-regular);
}

.btn-test {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--el-text-color-regular);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-small);
  background: transparent;
  padding: 2px 10px;
  cursor: pointer;
  transition: all 150ms ease;
}

.btn-test:hover:not(:disabled) {
  color: var(--el-text-color-primary);
  border-color: var(--el-border-color-darker, #999);
}

.btn-test:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.table-empty,
.table-error {
  padding: 40px;
  text-align: center;
  font-size: 0.875rem;
  color: var(--el-text-color-secondary);
}

.table-error { color: var(--el-color-danger); }

.btn-retry {
  margin-left: 8px;
  font-size: 0.75rem;
  color: var(--el-color-primary);
  background: transparent;
  border: 1px solid var(--el-color-primary);
  border-radius: var(--el-border-radius-small);
  padding: 2px 10px;
  cursor: pointer;
}

/* ── 网关配置 ── */
.gateway-panel {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding-bottom: 24px;
  max-width: 720px;
}

.gateway-status-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
  gap: 12px;
}

.status-card {
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  padding: 14px 16px;
  background: var(--el-bg-color-page);
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.card-label {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.card-value {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  word-break: break-all;
}

.card-value.mono {
  font-family: var(--font-family-mono, ui-monospace, monospace);
  font-size: 0.8125rem;
}

.card-sub {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.state-on { color: var(--el-color-success); }
.state-off { color: var(--el-text-color-secondary); }

.token-masked {
  font-family: var(--font-family-mono, ui-monospace, monospace);
  color: var(--el-color-primary);
}

.token-none { color: var(--el-text-color-secondary); font-weight: 400; }

.btn-copy {
  align-self: flex-start;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--el-color-primary);
  border: 1px solid var(--el-color-primary);
  border-radius: var(--el-border-radius-small);
  background: transparent;
  padding: 2px 10px;
  cursor: pointer;
  transition: all 150ms ease;
}

.btn-copy:hover:not(:disabled) {
  background: var(--el-color-primary-light-9);
}

.btn-copy:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.gateway-form {
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  padding: 20px;
  background: var(--el-bg-color-page);
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.form-title {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.form-desc {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  margin: 0;
}

.form-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.form-label {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--el-text-color-primary);
  width: 88px;
  flex-shrink: 0;
}

.port-input { width: 160px; }
.host-input { width: 240px; }
.token-input { width: 280px; }

.form-hint {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.row-clear-token {
  padding-left: 100px;
}

.form-actions {
  padding-left: 100px;
}

/* ── 外部服务器（v2.1.0） ── */
.external-section {
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  padding: 20px;
  background: var(--el-bg-color-page);
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.external-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.external-title-area {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.mono-inline {
  font-family: var(--font-family-mono, ui-monospace, monospace);
  font-size: 0.75rem;
  color: var(--el-color-primary);
  background: var(--el-fill-color-light);
  border-radius: 4px;
  padding: 1px 6px;
}

.external-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 60px;
}

.external-empty {
  padding: 20px;
  text-align: center;
  font-size: 0.8125rem;
  color: var(--el-text-color-secondary);
  border: 1px dashed var(--el-border-color);
  border-radius: var(--el-border-radius-base);
}

.external-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 14px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  transition: background 150ms ease;
}

.external-item:hover { background: var(--el-fill-color); }
.external-item.item-off { opacity: 0.55; }

.external-item-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.external-item-name {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.external-name-text {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.external-item-sub {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.external-toolcount {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.external-error-text {
  font-size: 0.75rem;
  color: var(--el-color-danger);
  max-width: 320px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.external-item-actions {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
  flex-wrap: wrap;
}

/* 外部服务器表单对话框 */
.external-form-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.external-form-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.external-form-label {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--el-text-color-primary);
  width: 76px;
  flex-shrink: 0;
}

.external-form-control {
  flex: 1;
  min-width: 0;
}

.external-form-hint {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.external-form-help {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  margin: 0;
}

/* 外部工具对话框 */
.external-tools-empty {
  padding: 24px;
  text-align: center;
  font-size: 0.8125rem;
  color: var(--el-text-color-secondary);
}

.external-tool-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 10px 12px;
  border-bottom: 1px solid var(--el-border-color-light);
}

.external-tool-item:last-child { border-bottom: none; }

.external-tool-name {
  font-family: var(--font-family-mono, ui-monospace, monospace);
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.external-tool-desc {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.external-tools-dialog :deep(.el-dialog__body) {
  max-height: 60vh;
  overflow-y: auto;
}

/* ── 测试 Toast ── */
.test-toast-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.3);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.test-toast {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  padding: 20px;
  min-width: 320px;
  max-width: 480px;
  box-shadow: 0 10px 15px rgba(0, 0, 0, 0.1);
}

.toast-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.toast-icon { font-size: 1.25rem; font-weight: 700; }
.toast-success .toast-icon { color: var(--el-color-success); }
.toast-fail .toast-icon { color: var(--el-color-danger); }

.toast-title {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  flex: 1;
}

.toast-close {
  font-size: 1.25rem;
  color: var(--el-text-color-secondary);
  background: none;
  border: none;
  cursor: pointer;
  line-height: 1;
  padding: 0;
}

.toast-body {
  font-size: 0.8125rem;
  color: var(--el-text-color-regular);
}

.toast-body p { margin: 0; }

.toast-duration {
  margin-top: 8px !important;
  color: var(--el-text-color-secondary);
  font-size: 0.75rem;
}
</style>
