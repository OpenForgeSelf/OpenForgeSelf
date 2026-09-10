<template>
  <!-- 顶层容器：三栏行布局，h-full + min-h-0 + overflow-hidden 保证内部栏各自滚动（从顶层容器约束高度） -->
  <div class="agent flex h-full min-h-0 overflow-hidden bg-bg-page text-text">
    <!-- 左栏（业界主流：会话/Agent 列表居左，对齐 ChatGPT/Claude 侧栏）：会话与统计；可折叠 -->
    <SessionPanel
      v-if="!leftCollapsed"
      :session-id="sessionId"
      :message-count="messageCount"
      :tool-call-count="toolCallCount"
      :token-text="tokenText"
      :agents="agents"
      :active-agent-id="activeAgentId"
      @new-session="startNewSession"
      @activate-agent="activateAgent"
      @edit-agent="onEditAgent"
      @new-agent="onNewAgent"
    />

    <!-- 中栏：项目文件编辑（可选） + 完整聊天区；min-h-0 链（agent__main → chat）是滚动修复关键 -->
    <div class="agent__main flex min-h-0 min-w-0 flex-1 flex-col">
      <FileEditor
        v-if="editingFile"
        :file="editingFile"
        :saving="savingFile"
        :hint="editorHint"
        :is-error="editorHintError"
        @save="saveFile"
        @close="closeFile"
      />
      <ChatPanel
        :messages="messages"
        :models="models"
        :selected-model-id="selectedModelId"
        :sending="sending"
        :error="error"
        :version="version"
        :tools="agentTools"
        :skills="agentSkills"
        :agents="agents"
        :active-agent-id="activeAgentId"
        :project-dir="projectDir"
        :selected-tool-names="selectedToolNames"
        :selected-skill-ids="selectedSkillIds"
        :left-collapsed="leftCollapsed"
        :right-collapsed="rightCollapsed"
        :run-card="runCard"
        @update:selected-model-id="onModelChange"
        @update:active-agent-id="onActiveAgentChange"
        @update:selected-tool-names="onToolNamesChange"
        @update:selected-skill-ids="onSkillIdsChange"
        @send="sendMessage"
        @stop="stopSending"
        @select-directory="onSelectDirectory"
        @clear-directory="onClearDirectory"
        @toggle-left="leftCollapsed = !leftCollapsed"
        @toggle-right="rightCollapsed = !rightCollapsed"
        @open-runs="runPanelVisible = true"
      />
    </div>

    <!-- 右栏（对齐 Cursor 上下文面板位）：AI 上下文；可折叠 -->
    <ContextPanel
      v-if="!rightCollapsed"
      :project-dir="projectDir"
      @select-directory="onSelectDirectory"
      @open-file="onOpenFile"
    />

    <!-- Agent 编辑对话框（新建 / 完整编辑 / 删除） -->
    <AgentEditDialog
      :agent="editingAgent"
      :visible="editDialogVisible"
      @update:visible="editDialogVisible = $event"
      @saved="onAgentSaved"
      @deleted="onAgentDeleted"
    />

    <!-- 执行记录面板（029 计划驱动：Run 列表 / 详情 / 继续 / 介入） -->
    <RunRecordPanel
      :visible="runPanelVisible"
      :session-id="sessionId"
      @update:visible="runPanelVisible = $event"
      @resume="onResumeRun"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * AIAgent 插件自带界面（spec 010 试点）。
 *
 * 形态：三栏完整聊天界面（按业界主流重排——会话/Agent 居左、聊天居中、上下文居右，
 * 对齐 ChatGPT/Claude 左侧栏 + Cursor 右侧上下文面板位）。2026-09-09 布局重构：
 * - **左右两栏互换**：左栏 = 会话与统计（SessionPanel），右栏 = AI 上下文（ContextPanel）。
 * - **从顶层容器重排**：`.agent` 用 Tailwind `flex h-full min-h-0 overflow-hidden` 直接约束高度，
 *   `agent__main` 与 `chat` 组成 **min-h-0 链**，修复长回复后聊天区不滚动、composer 底部被裁。
 * - **样式规范**：布局走 Tailwind 工具类（颜色映射 --el-* 变量），组件用饿了么（ElScrollbar/
 *   ElButton/ElTag/ElProgress 经共享桥取宿主同一份实例），减少造轮子；自定义 CSS 只留铸造主题细节。
 *
 * 设计约束（重要）：
 * - **插件用 EP 组件须显式 import**：宿主 unplugin-vue-components 不处理插件预编译产物，
 *   `<ElXxx>` 显式 `import { ElXxx } from 'element-plus'` 后经 import map 解析到宿主同一份实例；
 *   组件样式由插件 index.ts 自备（element-plus 各组件 style/css），颜色走宿主 --el-* 变量。
 * - 不引用宿主的 `@/stores`、`@/services`、`@/components`（`@/` 是宿主别名，插件无从解析），
 *   数据一律通过 HTTP 直连后端（见 http.ts）。
 * - vue 为外部依赖，经宿主页面的 import map 解析到宿主同一份实例，避免 Vue 双实例。
 *
 * 状态说明：消息与会话状态提升到本组件持有，是为了让左栏「会话统计」显示真实数据，
 * 避免出现两处独立状态导致统计与界面不一致。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import {
  apiGet,
  apiPost,
  getRunDetail,
  resumeAgentRun,
  streamAgentChat,
  streamAgentRun,
  withQuery,
  type AgentUsage,
  type RunStreamHandlers,
} from './http'
import type {
  AgentDefinition,
  AgentPlan,
  AgentRunDto,
  AgentTool,
  AIModel,
  ChatMessage,
  EditingFile,
  PlanRunCard,
  PlanStepView,
  ProjectSkillItem,
  ToolEvent,
} from './types'
import { stepStatusName } from './types'
import ContextPanel from './components/ContextPanel.vue'
import ChatPanel from './components/ChatPanel.vue'
import FileEditor from './components/FileEditor.vue'
import SessionPanel from './components/SessionPanel.vue'
import AgentEditDialog from './components/AgentEditDialog.vue'
import RunRecordPanel from './components/RunRecordPanel.vue'

/** 插件 id，与 plugin.json 的 Id 对齐。 */
const PLUGIN_ID = 'ai-agent'
/** 选中模型的本地持久化键（与宿主内置页面一致，便于共享选择）。 */
const LS_KEY_MODEL = 'forgeself-agent-current-model'
/** 当前会话 id 的本地持久化键。 */
const LS_KEY_SESSION = 'forgeself-agent-session-id'
/** 当前激活 Agent 的本地持久化键。 */
const LS_KEY_AGENT = 'forgeself-agent-current-agent'
/** 当前项目工作目录的本地持久化键。 */
const LS_KEY_PROJECT = 'forgeself-agent-project-dir'

/** 插件版本（展示在浏览器标签/调试信息，设计原型无此元素，故不放在标题旁）。 */
const version = ref('')
/** 可用模型列表。 */
const models = ref<AIModel[]>([])
/** 当前选中的模型 id。 */
const selectedModelId = ref('')
/** 当前会话 id。 */
const sessionId = ref('')
/** 消息列表。 */
const messages = ref<ChatMessage[]>([])
/** 是否正在等待后端回复。 */
const sending = ref(false)
/** 错误提示。 */
const error = ref('')

/** Agent 列表（来自 GET /api/agents）。 */
const agents = ref<AgentDefinition[]>([])
/** 当前激活 Agent 的 id（默认取第一个通用 Agent 或列表首项）。 */
const activeAgentId = ref('')

/** Stage 4：正在编辑的 Agent（编辑提示词对话框）。 */
const editingAgent = ref<AgentDefinition | null>(null)
const editDialogVisible = ref(false)

/** 当前项目工作目录（绝对路径，一个目录视为一个项目）。 */
const projectDir = ref('')
/** 正在编辑的项目文件；为空表示编辑器关闭。 */
const editingFile = ref<EditingFile | null>(null)
const savingFile = ref(false)
/** 编辑器顶部状态提示（成功/失败文案）。 */
const editorHint = ref('')
const editorHintError = ref(false)

/** composer 🔧 可选工具：后端 /api/ai-agent/chat/tools 全量按 pluginId 白名单过滤（与 RunAgentLoopAsync 同源）。 */
const agentTools = ref<AgentTool[]>([])
/** composer ⚡ 可选技能：已选项目目录取项目技能，否则全局技能（与 ContextPanel 同源）。 */
const agentSkills = ref<ProjectSkillItem[]>([])
/** 本会话已选工具名（🔧 多选，发送后清空——所见即所发）。 */
const selectedToolNames = ref<string[]>([])
/** 本会话已选技能 id（⚡ 多选，发送后清空）。 */
const selectedSkillIds = ref<string[]>([])
/** 左栏（会话/Agent 列表）是否折叠（折叠后单栏接近主流对话体验）。 */
const leftCollapsed = ref(false)
/** 右栏（AI 上下文：目录/工具/技能/记忆）是否折叠。默认显示（展开），内部各分组默认折叠。 */
const rightCollapsed = ref(false)
/** 当前发送的 AbortController（composer 发送按钮发送中变 ■ 停止用）。 */
const sendAbort = ref<AbortController | null>(null)
/** 计划驱动执行（029）：中栏步骤进度卡的实时运行状态。 */
const runCard = ref<PlanRunCard | null>(null)
/** 执行记录面板（029）是否打开。 */
const runPanelVisible = ref(false)
/** 计划驱动执行的 AbortController（停止 = 中断 SSE 流）。 */
const runAbort = ref<AbortController | null>(null)

/** 当前激活 Agent（用于判断执行模式：free 自由循环 / plan 计划驱动）。 */
const activeAgent = computed(() => agents.value.find((a) => a.id === activeAgentId.value) ?? null)

/** 消息条数（真实统计）。 */
const messageCount = computed(() => messages.value.length)
/** 工具调用次数（真实统计：累加各条消息的工具事件数，兼容非流式的 toolCalls）。 */
const toolCallCount = computed(() =>
  messages.value.reduce((sum, m) => sum + (m.toolEvents?.length ?? m.toolCalls?.length ?? 0), 0)
)
/** 最近一次回复的 token 用量（流式 usage 事件填充；无则显示占位符）。 */
const lastUsage = ref<AgentUsage | null>(null)
/** token 用量文案；后端未返回时如实显示占位符。 */
const tokenText = computed(() => {
  const u = lastUsage.value
  if (!u) return '—'
  const total = u.totalTokens ?? ((u.promptTokens ?? 0) + (u.completionTokens ?? 0))
  return `${total} tok`
})

/** 生成一个新的会话 id（简单时间戳 + 随机串，避免引入 uuid 依赖）。 */
function newSessionId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`
}

/** 读取或创建会话 id，并持久化。 */
function ensureSessionId(): string {
  let id = localStorage.getItem(LS_KEY_SESSION)
  if (!id) {
    id = newSessionId()
    localStorage.setItem(LS_KEY_SESSION, id)
  }
  return id
}

/** 拉取插件版本与可用模型。 */
async function loadMeta() {
  // 版本：/api/plugin（注意是单数）
  try {
    const list = await apiGet<Array<{ id?: string; version?: string }>>('/api/plugin')
    version.value = list?.find((p) => p.id === PLUGIN_ID)?.version ?? ''
  } catch {
    // 版本不是核心链路，失败时静默降级为空
  }

  try {
    const data = await apiGet<{ groups?: Array<{ providerName?: string; models?: AIModel[] }> }>(
      '/api/ai-models?enabledOnly=true'
    )
    const flat: AIModel[] = []
    for (const g of data?.groups ?? []) {
      for (const m of g.models ?? []) flat.push({ ...m, providerName: m.providerName ?? g.providerName })
    }
    models.value = flat

    const saved = localStorage.getItem(LS_KEY_MODEL)
    if (saved && flat.some((m) => m.chatModelId === saved)) {
      selectedModelId.value = saved
    } else if (flat.length > 0) {
      selectedModelId.value = flat[0]?.chatModelId ?? ''
      localStorage.setItem(LS_KEY_MODEL, selectedModelId.value)
    }
  } catch {
    models.value = []
  }
}

/** 加载 Agent 列表（真实数据：GET /api/agents）。 */
async function loadAgents() {
  try {
    const list = await apiGet<AgentDefinition[]>('/api/agents')
    agents.value = list ?? []

    // 优先恢复上次选中的 Agent；否则取「通用助手」，再退回列表首项。
    const saved = localStorage.getItem(LS_KEY_AGENT)
    if (saved && agents.value.some((a) => a.id === saved)) {
      activeAgentId.value = saved
    } else {
      const fallback =
        agents.value.find((a) => a.id === 'agent.generalist') ?? agents.value[0]
      activeAgentId.value = fallback?.id ?? ''
      if (fallback?.id) localStorage.setItem(LS_KEY_AGENT, fallback.id)
    }
  } catch {
    agents.value = []
  }
}

/** 切换当前激活 Agent 并持久化（右栏 Agent 列表点击）。 */
function activateAgent(agentId: string) {
  activeAgentId.value = agentId
  if (agentId) localStorage.setItem(LS_KEY_AGENT, agentId)
}

/** 打开 Agent 编辑对话框（编辑模式）。 */
function onEditAgent(agent: AgentDefinition) {
  editingAgent.value = agent
  editDialogVisible.value = true
}

/** 打开 Agent 编辑对话框（新建模式：agent=null 走默认表单）。 */
function onNewAgent() {
  editingAgent.value = null
  editDialogVisible.value = true
}

/** 保存 Agent 后刷新列表（让编辑后的提示词/配置即时生效）。 */
async function onAgentSaved(_updated: AgentDefinition) {
  await loadAgents()
}

/** 删除 Agent 后刷新列表；若删除的是当前激活项，交给 loadAgents 的 fallback 重选。 */
async function onAgentDeleted(agentId: string) {
  if (activeAgentId.value === agentId) {
    localStorage.removeItem(LS_KEY_AGENT)
    activeAgentId.value = ''
  }
  await loadAgents()
}

/** 加载 composer 🔧 可选工具列表（与后端 RunAgentLoopAsync 的 ResolveOwnToolDefinitions 白名单同源）。 */
async function loadAgentTools() {
  try {
    const data = await apiGet<{ tools?: AgentTool[] }>('/api/ai-agent/chat/tools')
    const all = data?.tools ?? []
    agentTools.value = all.filter((t) => t.pluginId === 'ai-agent' || t.pluginId === 'memory-system')
  } catch {
    agentTools.value = []
  }
}

/** 加载 composer ⚡ 可选技能列表（已选项目目录取项目技能，否则全局技能）。 */
async function loadAgentSkills() {
  try {
    const src = projectDir.value ? '/api/project/skills' : '/api/skills'
    agentSkills.value = (await apiGet<ProjectSkillItem[]>(src)) ?? []
  } catch {
    agentSkills.value = []
  }
}

// 目录变化（成功加载/清除）时刷新 composer 技能列表（技能源随项目变化）。
watch(projectDir, () => {
  void loadAgentSkills()
})

/** 更新 composer 🔧 已选工具名集合（状态上移，发送时组装进请求）。 */
function onToolNamesChange(v: string[]) {
  selectedToolNames.value = v
}

/** 更新 composer ⚡ 已选技能 id 集合。 */
function onSkillIdsChange(v: string[]) {
  selectedSkillIds.value = v
}

/** 更新 composer 🤖 激活 Agent（与右栏切换共用持久化）。 */
function onActiveAgentChange(agentId: string) {
  activateAgent(agentId)
}

/** 移除目录 chip：仅清客户端状态（后端 workspace root 维持会话级，无清空端点；下次加载目录即重置）。 */
function onClearDirectory() {
  projectDir.value = ''
  localStorage.removeItem(LS_KEY_PROJECT)
}

/** 停止当前流式回复（composer 发送按钮发送中变 ■）。 */
function stopSending() {
  sendAbort.value?.abort()
}

/** 选择项目工作目录：写后端 + 更新本地状态 + 持久化。 */
async function onSelectDirectory(path: string) {
  try {
    const data = await apiPost<{ root?: string }>('/api/project/directory', { path })
    projectDir.value = data?.root ?? path
    if (projectDir.value) localStorage.setItem(LS_KEY_PROJECT, projectDir.value)
    editorHint.value = ''
  } catch (e) {
    editorHint.value = e instanceof Error ? e.message : String(e)
    editorHintError.value = true
  }
}

/** 点击文件：读取内容并打开编辑器。 */
async function onOpenFile(file: { path: string; name: string }) {
  try {
    const data = await apiGet<{ content?: string }>(withQuery('/api/project/file', { path: file.path }))
    editingFile.value = { path: file.path, name: file.name, content: data?.content ?? '' }
    editorHint.value = ''
    editorHintError.value = false
  } catch (e) {
    editorHint.value = e instanceof Error ? e.message : String(e)
    editorHintError.value = true
  }
}

/** 保存当前文件到后端。 */
async function saveFile(content: string) {
  if (!editingFile.value || savingFile.value) return
  savingFile.value = true
  try {
    await apiPost('/api/project/file', { path: editingFile.value.path, content })
    editingFile.value.content = content
    editorHint.value = '已保存'
    editorHintError.value = false
  } catch (e) {
    editorHint.value = e instanceof Error ? e.message : String(e)
    editorHintError.value = true
  } finally {
    savingFile.value = false
  }
}

/** 关闭编辑器。 */
function closeFile() {
  editingFile.value = null
  editorHint.value = ''
  editorHintError.value = false
}

/** 加载当前会话的历史消息（插件自己的聊天存储）。 */
async function loadHistory() {
  if (!sessionId.value) return
  try {
    const list = await apiGet<ChatMessage[]>(
      `/api/ai-agent/chat/history/${encodeURIComponent(sessionId.value)}?limit=50`
    )
    messages.value = (list ?? []).map((m, i) => ({ ...m, id: m.id ?? `h-${i}` }))
  } catch {
    // 新会话尚无历史，视为空即可
    messages.value = []
  }
}

/** 切换模型并持久化。 */
function onModelChange(value: string) {
  selectedModelId.value = value
  if (value) localStorage.setItem(LS_KEY_MODEL, value)
}

/** 发送消息：依据所选 Agent 执行模式分流（029）——plan 走计划驱动 runs SSE，free 走聊天流。 */
async function sendMessage(text: string) {
  if (sending.value) return
  if (activeAgent.value?.executionMode === 'plan') {
    await sendPlanMessage(text)
    return
  }
  await sendFreeMessage(text)
}

/** 自由循环（FreeLoop）：乐观插用户气泡 + assistant 占位，调插件流式接口，逐 token 追加并实时展示工具调用。 */
async function sendFreeMessage(text: string) {
  if (sending.value) return
  error.value = ''
  sending.value = true

  // 快照本次上下文选择并清空（所见即所发：工具/技能随消息发出；目录/模型/Agent 会话级保留）。
  const toolNames = [...selectedToolNames.value]
  const skillIds = [...selectedSkillIds.value]
  selectedToolNames.value = []
  selectedSkillIds.value = []

  // 停止按钮的取消句柄（composer 发送按钮发送中变 ■）。
  const controller = new AbortController()
  sendAbort.value = controller

  // 乐观追加用户消息，保证界面即时反馈
  messages.value.push({ id: `u-${Date.now()}`, role: 'user', content: text })

  // assistant 占位消息：**必须 reactive 包装**。push 进 ref 数组后若继续持有原始对象引用，
  // 流式期间对该引用的变更（content += chunk / toolEvents.push）不经过 proxy → 不触发渲染，
  // 界面会停在「…」直到 sending 翻转才整段出现（Stage 1 实测踩坑）；reactive 后逐 token 触发。
  const assistantMsg = reactive<ChatMessage>({
    id: `a-${Date.now()}`,
    role: 'assistant',
    content: '',
    toolEvents: [],
  })
  messages.value.push(assistantMsg)

  /** 移除仍是空内容的 assistant 占位（避免出错后界面永久停在「…」）。 */
  function dropEmptyPlaceholder() {
    if (!assistantMsg.content && (assistantMsg.toolEvents?.length ?? 0) === 0) {
      messages.value = messages.value.filter((m) => m !== assistantMsg)
    }
  }

  /** 标记最后一个仍在 pending 的工具事件为已完成并填结果。 */
  function settleToolEvent(e: { name?: string; result?: string; success?: boolean }) {
    const events = assistantMsg.toolEvents ?? []
    // 从后往前找同名且 pending 的事件
    for (let i = events.length - 1; i >= 0; i--) {
      const ev = events[i]
      if (ev && ev.pending && ev.name === e.name) {
        ev.result = e.result
        ev.success = e.success
        ev.pending = false
        return
      }
    }
    // 没找到（异常情况）则补一条
    events.push({ name: e.name, result: e.result, success: e.success, pending: false })
  }

  try {
    await streamAgentChat(
      {
        sessionId: sessionId.value,
        message: text,
        chatModelId: selectedModelId.value || undefined,
        agentId: activeAgentId.value || undefined,
        enabledToolNames: toolNames.length > 0 ? toolNames : undefined,
        skillIds: skillIds.length > 0 ? skillIds : undefined,
      },
      {
        onContent: (chunk) => {
          assistantMsg.content += chunk
        },
        onToolCall: (e) => {
          ;(assistantMsg.toolEvents as ToolEvent[]).push({
            name: e.name,
            args: e.arguments,
            pending: true,
          })
        },
        onToolResult: (e) => {
          settleToolEvent(e)
        },
        onUsage: (usage) => {
          if (usage) lastUsage.value = usage
        },
        onDone: (e) => {
          if (e.usage) lastUsage.value = e.usage
          if (e.responseId != null) assistantMsg.id = e.responseId
        },
        onError: (msg) => {
          error.value = msg
          // 流中出错时占位消息可能仍是空的（无内容、无工具事件），
          // 不移除会永久停在「…」打字指示（错误已由 error 条呈现）。
          dropEmptyPlaceholder()
        },
      },
      controller.signal,
    )
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') {
      // 用户主动停止：保留已生成内容，不显示错误，仅移除仍为空的占位。
      dropEmptyPlaceholder()
    } else {
      error.value = e instanceof Error ? e.message : String(e)
      // 请求级失败（未建立流）：移除空的 assistant 占位，避免残留空气泡
      dropEmptyPlaceholder()
    }
  } finally {
    sending.value = false
    sendAbort.value = null
  }
}

/* ------------------------------------------------------------------ */
/* 计划驱动执行（029）：Plan 模式 SSE 分流 + 步骤进度卡 + 恢复             */
/* ------------------------------------------------------------------ */

/** 由 Run.planJson（camelCase Plan DSL）重建 Plan 骨架；解析失败回退空。 */
function planFromRunJson(planJson?: string): { plan: AgentPlan | null; steps: PlanStepView[] } {
  if (!planJson) return { plan: null, steps: [] }
  try {
    const p = JSON.parse(planJson) as AgentPlan
    return {
      plan: p,
      steps: (p.steps ?? []).map((s, i) => ({
        index: i,
        id: s.id,
        name: s.name,
        objective: s.objective,
        status: 'pending',
      })),
    }
  } catch {
    return { plan: null, steps: [] }
  }
}

/**
 * 构造计划驱动 SSE 处理器（创建与恢复共用）。
 * 事件 → 步骤卡状态增量维护：plan_created 重建步骤骨架、step_started/step_completed 推进、
 * run_stuck 置卡住、done 收尾交付、error 记入错误条。
 */
function buildRunHandlers(): RunStreamHandlers {
  const setStep = (index: number, patch: Partial<PlanStepView>) => {
    const card = runCard.value
    if (!card) return
    let st = card.steps.find((s) => s.index === index)
    if (!st) {
      st = { index, status: 'pending' }
      card.steps.push(st)
      card.steps.sort((a, b) => a.index - b.index)
    }
    Object.assign(st, patch)
  }
  return {
    onPlanCreated: (payload) => {
      const card = runCard.value
      if (!card) return
      card.plan = payload.plan
      card.status = 'planning'
      card.steps = (payload.plan?.steps ?? []).map((s, i) => ({
        index: i,
        id: s.id,
        name: s.name,
        objective: s.objective,
        status: 'pending',
      }))
    },
    onStepStarted: (payload) => {
      if (!runCard.value) return
      runCard.value.status = 'running'
      setStep(payload.stepIndex, {
        id: payload.stepId,
        name: payload.name,
        objective: payload.objective,
        status: 'running',
      })
    },
    onStepCompleted: (payload) => {
      setStep(payload.stepIndex, { status: 'completed', output: payload.output })
    },
    onRunStuck: (payload) => {
      const card = runCard.value
      if (!card) return
      card.status = 'stuck'
      card.stuckReason = payload.reason
      setStep(payload.stepIndex, { status: 'stuck', stuckReason: payload.reason })
    },
    onContent: (chunk) => {
      // 步骤内增量输出累积到当前 running 步骤（与消息流不同：计划模式中间产出归步骤卡）。
      const card = runCard.value
      if (!card) return
      const running = card.steps.find((s) => s.status === 'running')
      if (running) running.output = (running.output ?? '') + chunk
    },
    onUsage: (usage) => {
      if (usage) lastUsage.value = usage
    },
    onDone: (content) => {
      const card = runCard.value
      if (card) card.status = 'completed'
      messages.value.push({ id: `a-${Date.now()}`, role: 'assistant', content })
    },
    onError: (msg) => {
      error.value = msg
      if (runCard.value) runCard.value.error = msg
    },
  }
}

/** 计划驱动发送（029）：调 POST /api/ai-agent/runs SSE，全程驱动步骤进度卡，完成后追加交付消息。 */
async function sendPlanMessage(text: string) {
  error.value = ''
  sending.value = true

  // 乐观插用户消息，并初始化步骤卡（等待规划）。
  messages.value.push({ id: `u-${Date.now()}`, role: 'user', content: text })
  runCard.value = { runId: 0, plan: null, status: 'pending', steps: [] }

  const controller = new AbortController()
  runAbort.value = controller

  try {
    await streamAgentRun(
      {
        sessionId: sessionId.value,
        agentId: activeAgentId.value,
        taskInput: text,
        chatModelId: selectedModelId.value || undefined,
      },
      buildRunHandlers(),
      controller.signal,
    )
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') {
      // 用户停止：保留现场（Run 后端仍为运行态，可从执行记录面板取消/重开）。
    } else {
      error.value = e instanceof Error ? e.message : String(e)
      if (runCard.value) runCard.value.error = error.value
    }
  } finally {
    sending.value = false
    runAbort.value = null
  }
}

/** 执行记录面板「继续」：以现有 Run 从当前步骤恢复（POST runs/{id}/resume，SSE）。 */
async function onResumeRun(run: AgentRunDto) {
  if (sending.value) return
  error.value = ''
  sending.value = true
  runPanelVisible.value = false

  // 从 run.planJson 重建骨架（resume 流不重发 plan_created），再用库内步骤状态覆盖历史。
  const { plan, steps } = planFromRunJson(run.planJson)
  runCard.value = { runId: run.id, plan, status: 'running', steps, stuckReason: undefined, error: undefined }
  try {
    const detail = await getRunDetail(run.id)
    if (detail?.steps && detail.steps.length > 0) {
      runCard.value.steps = detail.steps.map((s) => ({
        index: s.stepIndex,
        id: s.stepId,
        name: s.name,
        objective: s.objective,
        status: stepStatusName(s.status),
        output: s.outputJson ?? undefined,
        stuckReason: s.stuckReason ?? undefined,
      }))
    }
  } catch {
    // 详情加载失败不阻断：以 planJson 骨架继续，SSE 事件仍会推进。
  }

  const controller = new AbortController()
  runAbort.value = controller
  try {
    await resumeAgentRun(run.id, buildRunHandlers(), controller.signal)
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') {
      // 用户停止：保留现场。
    } else {
      error.value = e instanceof Error ? e.message : String(e)
      if (runCard.value) runCard.value.error = error.value
    }
  } finally {
    sending.value = false
    runAbort.value = null
  }
}

/** 新建会话：生成新 id、清空消息与错误。 */
function startNewSession() {
  const id = newSessionId()
  localStorage.setItem(LS_KEY_SESSION, id)
  sessionId.value = id
  messages.value = []
  error.value = ''
}

onMounted(async () => {
  sessionId.value = ensureSessionId()
  await loadMeta()
  await loadAgents()
  await loadAgentTools()
  await loadAgentSkills()
  // 恢复上次选定的工作目录（若有），同步到后端供文件工具使用
  const savedDir = localStorage.getItem(LS_KEY_PROJECT)
  if (savedDir) {
    projectDir.value = savedDir
    void onSelectDirectory(savedDir)
  }
  await loadHistory()
})
</script>
