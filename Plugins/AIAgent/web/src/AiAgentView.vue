<template>
  <!-- 顶层容器：三栏行布局，h-full + min-h-0 + overflow-hidden 保证内部栏各自滚动（从顶层容器约束高度） -->
  <div class="agent flex h-full min-h-0 overflow-hidden bg-bg-page text-text">
    <!-- 左栏（业界主流：会话/Agent 列表居左，对齐 ChatGPT/Claude 侧栏）：会话与统计；可折叠 -->
    <SessionPanel
      v-if="!leftCollapsed"
      :session-id="sessionId"
      :sessions="sessions"
      :message-count="messageCount"
      :tool-call-count="toolCallCount"
      :token-text="tokenText"
      :agents="agents"
      :active-agent-id="activeAgentId"
      @new-session="startNewSession"
      @select-session="selectSession"
      @archive-session="onArchiveSession"
      @activate-agent="activateAgent"
      @edit-agent="onEditAgent"
      @new-agent="onNewAgent"
    />

    <!-- 中栏：项目文件编辑（可选） + 聊天区（agent-core 组件）；min-h-0 链（agent__main → chat）是滚动修复关键 -->
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
      <div class="chat flex min-h-0 min-w-0 flex-1 flex-col">
        <!-- 顶部条（v1.8.0 起由本页提供，组件自身标题栏已隐藏）：折叠 + 标题 + 版本徽标 + 状态 + 执行记录 -->
        <header class="chat__bar">
          <div class="chat__bar-left">
            <button
              type="button"
              class="chat__fold"
              :title="leftCollapsed ? '展开左栏' : '收起左栏'"
              @click="leftCollapsed = !leftCollapsed"
            >
              <!-- 收起/展开随栏状态自动切换：箭头语义暧昧，改用 EP Fold/Expand，点开/收起意图一目了然 -->
              <component :is="leftCollapsed ? Expand : Fold" class="chat__fold-ico" />
            </button>
            <span class="chat__title">
              AI Agent
              <!-- 插件版本徽标：用户要求「标题后显示插件版本」；
                   设计原型标题旁无此元素，故作为追加元素挂在这里，既满足需求又不破坏原型布局。 -->
              <span v-if="version" class="chat__version">v{{ version }}</span>
            </span>
            <span class="chat__status">
              <span class="chat__dot" :class="{ 'chat__dot--busy': sending }"></span>
              {{ sending ? '思考中' : '就绪' }}
            </span>
          </div>

          <div class="chat__bar-right">
            <!-- 执行记录（029 计划驱动）：打开 Run 列表/详情面板 -->
            <button
              type="button"
              class="chat__fold"
              title="执行记录（计划驱动 Run 列表 / 详情 / 介入）"
              @click="runPanelVisible = true"
            >
              <List class="chat__fold-ico" />
            </button>
            <button
              type="button"
              class="chat__fold"
              :title="rightCollapsed ? '展开右栏' : '收起右栏'"
              @click="rightCollapsed = !rightCollapsed"
            >
              <component :is="rightCollapsed ? Expand : Fold" class="chat__fold-ico" />
            </button>
          </div>
        </header>

        <!-- agent-core 聊天组件（v1.8.0 替换 ChatPanel）：消息区 + 输入框由组件承载，
             计划步骤卡/工具卡片等本批暂不渲染（记 TODO，功能迁入组件后恢复） -->
        <ChatWidget
          ref="chatRef"
          class="min-h-0 min-w-0 flex-1"
          :config="agentConfig"
          :control="chatControl"
          @state-changed="onChatStateChanged"
        />
      </div>
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
import { computed, onBeforeUnmount, onMounted, reactive, ref, unref, watch, watchEffect } from 'vue'
// ElMessageBox：归档前的二次确认弹窗（宿主已全局引入 el-message-box.css，无需插件另引样式）。
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  apiGet,
  apiPost,
  fetchSessions,
  getRunDetail,
  resumeAgentRun,
  withQuery,
  type AgentUsage,
  type RunStreamHandlers,
} from './http'
import { archiveSessionWithConfirm } from './sessionArchive'
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
  SessionSummary,
  ToolEvent,
} from './types'
import { stepStatusName } from './types'
import ContextPanel from './components/ContextPanel.vue'
import FileEditor from './components/FileEditor.vue'
import SessionPanel from './components/SessionPanel.vue'
import AgentEditDialog from './components/AgentEditDialog.vue'
import RunRecordPanel from './components/RunRecordPanel.vue'
// v1.8.0：中栏聊天区由 agent-core 组件（ai-assistant-widget 迁移）承载
import ChatWidget from './agent-core/components/ChatWidget.vue'
import { buildAgentChatConfig, createHostAppearance } from './agentBridge'
import {
  MAX_FILES,
  buildCommandGroups,
  collectProjectFiles,
  joinRel,
  pickItem,
  shouldDescend,
  shouldKeepFile,
} from './composer'
import {
  PROJECT_CLEAR_ID,
  buildAskConfig,
  buildContextBarConfig,
  todosToAskItem,
  type TodoDtoLike,
} from './composerPanels'
import type { ComposerItem } from './composer'
import type { AccessMode } from './composerContext'
import type { AskItem } from './composerContext'
import type { HostAppearance } from './agentBridge'
import type { AgentChatConfig, ChatWidgetExpose } from './agent-core/types'
import { Expand, Fold, List } from '@element-plus/icons-vue'

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
/** 041 权限档位的本地持久化键（会话级设置：刷新后仍按上次的安全档发）。 */
const LS_KEY_ACCESS = 'forgeself-agent-access-mode'

/** 插件版本（展示在浏览器标签/调试信息，设计原型无此元素，故不放在标题旁）。 */
const version = ref('')
/** 可用模型列表。 */
const models = ref<AIModel[]>([])
/** 当前选中的模型 id。 */
const selectedModelId = ref('')
/** 当前会话 id。 */
const sessionId = ref('')
/** 历史会话列表（来自 GET /api/ai-agent/chat/sessions）。 */
const sessions = ref<SessionSummary[]>([])
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
/* ---------- 041 权限档位 + 图标行 + 待你动作 ---------- */
/** 权限档位（readonly 只读 / standard 标准 / full 完全访问），默认 standard（最安全可用档）。 */
const accessMode = ref<AccessMode>('standard')
/** 图标行当前展开的面板 key（null = 全关）；宿主要知道谁开着，才能算搜索过滤结果。 */
const ctxOpenKey = ref<string | null>(null)
/** 各面板搜索词（按 key 分开，切面板不串词）。 */
const ctxQueries = ref<Record<string, string>>({})
/** 待办清单（TodoTracker 插件，GET /api/todos），供输入框上方「待办」卡渲染。 */
const todos = ref<TodoDtoLike[]>([])
/** 已登记过的项目目录（本机 localStorage 累积），供项目面板直接点选。 */
const knownProjects = ref<string[]>([])
/**
 * 041 瞬时待你动作（审批 / 询问）。
 * 当前 AIAgent 后端**无 approval 端点**（实测 grep 全空），故恒为空数组；
 * 契约已就绪（config.ask + AskPanel），等后端接入即可直接出卡。
 */
const askItems = ref<AskItem[]>([])
/** 左栏（会话/Agent 列表）是否折叠（折叠后单栏接近主流对话体验）。 */
const leftCollapsed = ref(false)
/** 右栏（AI 上下文：目录/工具/技能/记忆）是否折叠。默认显示（展开），内部各分组默认折叠。 */
const rightCollapsed = ref(false)
/** 计划驱动执行（029）：中栏步骤进度卡的实时运行状态（本批组件不渲染步骤卡，数据源保留供执行记录/后续迁入）。 */
const runCard = ref<PlanRunCard | null>(null)
/** 执行记录面板（029）是否打开。 */
const runPanelVisible = ref(false)
/** 计划驱动执行的 AbortController（停止 = 中断 SSE 流）。 */
const runAbort = ref<AbortController | null>(null)

/** 当前激活 Agent（用于判断执行模式：free 自由循环 / plan 计划驱动）。 */
const activeAgent = computed(() => agents.value.find((a) => a.id === activeAgentId.value) ?? null)

/* ---------- agent-core 聊天组件（v1.8.0 替换 ChatPanel） ---------- */
/** 组件实例句柄（hydrate / isSending / messages / lastUsage）。 */
const chatRef = ref<ChatWidgetExpose | null>(null)
/** 组件 inline 形态的打开控制：常驻打开（inline 下 close 被组件拦截）。 */
const chatControl = reactive({ open: true })
/** 组件 config：transport 经 deps getter 每次发送取最新值（响应式源）。
 *  reactive 包裹使外观字段（darkMode/primaryColor）可被宿主主题切换实时驱动。 */
const agentConfig = reactive(
  buildAgentChatConfig({
    sessionId: () => sessionId.value,
    activeAgentId: () => activeAgentId.value,
    selectedModelId: () => selectedModelId.value,
    selectedToolNames: () => selectedToolNames.value,
    selectedSkillIds: () => selectedSkillIds.value,
    activeAgent: () => activeAgent.value,
    // 041 权限：getter 注入，transport 每次发送取最新档位
    accessMode: () => accessMode.value,
    allToolNames: () => agentTools.value.map((t) => t.name ?? '').filter((n) => !!n),
  })
) as AgentChatConfig

/** 宿主外观（深色/主色）同步到组件：跟随宿主主题，不使用组件自带的紫/白底默认值。 */
const hostAppearance = createHostAppearance()
const applyHostAppearance = (v: HostAppearance) => {
  agentConfig.darkMode = v.dark
  agentConfig.primaryColor = v.primaryColor
}
applyHostAppearance(hostAppearance.get())
const unsubscribeAppearance = hostAppearance.subscribe(applyHostAppearance)
onBeforeUnmount(() => {
  unsubscribeAppearance()
  hostAppearance.dispose()
})

/** 消息条数（真实统计；由组件 state-changed 事件驱动刷新）。 */
const messageCount = ref(0)
/** 工具调用次数（真实统计：累加各条消息的工具事件数，兼容非流式的 toolCalls）。 */
const toolCallCount = ref(0)
/** 最近一次回复的 token 用量（组件 lastUsage 上抛；无则显示占位符）。 */
const lastUsage = ref<AgentUsage | null>(null)
/** token 用量文案；后端未返回时如实显示占位符。 */
const tokenText = computed(() => {
  const u = lastUsage.value
  if (!u) return '—'
  const total = u.totalTokens ?? ((u.promptTokens ?? 0) + (u.completionTokens ?? 0))
  return `${total} tok`
})

/** 组件消息状态变化（发送开始/结束、会话切换灌入历史）：同步左栏统计与状态点。 */
function onChatStateChanged() {
  sending.value = chatRef.value?.isSending() ?? false
  // unref 兼容：defineExpose 暴露的 ref 在不同 Vue 版本/访问路径下可能已自动解包
  const allMsgs = (unref(chatRef.value?.messages) as ChatMessage[] | undefined) ?? []
  // 组件在空会话注入的欢迎消息（welcome 标记）不参与消息数统计
  const msgs = allMsgs.filter((m) => !(m as ChatMessage & { welcome?: boolean }).welcome)
  messageCount.value = msgs.length
  toolCallCount.value = msgs.reduce((sum, m) => sum + (m.toolEvents?.length ?? m.toolCalls?.length ?? 0), 0)
  lastUsage.value = (unref(chatRef.value?.lastUsage) as AgentUsage | null | undefined) ?? null
  // 一轮回复完成 / 会话切换后，左栏会话列表按后端真实状态刷新
  void loadSessions()
  // 041：Agent 可能改动待办，一轮结束后同步输入框上方的「待办」卡
  void loadTodos()
}

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

/**
 * 计划驱动运行流特殊工具：只由 PlanDriven 步骤循环经 extraTools 显式挂载，
 * 不进 FreeLoop，也不应出现在 composer 的手动勾选列表里（与后端 ResolveToolDefinitions 同源）。
 */
const RUN_FLOW_TOOL_NAMES = new Set(['submit_plan', 'complete_step', 'request_help'])

/** 加载 composer 🔧 可选工具列表（宿主 ToolRegistry 全量已注册工具）。 */
async function loadAgentTools() {
  try {
    const data = await apiGet<{ tools?: AgentTool[] }>('/api/ai-agent/chat/tools')
    const all = data?.tools ?? []
    // 不再按 pluginId 白名单过滤：万能工具（universal_tool）、执行命令（run_terminal_command）、
    // todo 工具（todo-tracker）等其它插件工具也要能勾选。
    // 后端语义：不勾选 = 挂默认作用域（本插件 + memory-system + design-system，防 prompt 撑爆）；
    // 勾选 = 显式意图，候选池放开到全注册表（见 AIAgentService.ResolveSelectableToolDefinitions）。
    agentTools.value = all
      .filter((t) => !!t.name && !RUN_FLOW_TOOL_NAMES.has(t.name))
      .slice()
      .sort(
        (a, b) =>
          (a.pluginId ?? '').localeCompare(b.pluginId ?? '') ||
          (a.name ?? '').localeCompare(b.name ?? ''),
      )
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

// 目录变化（成功加载/清除）时刷新 composer 技能列表（技能源随项目变化）
// 与 `@` 文件候选 —— 两者都随项目走。
// ★ 必须**等文件收完再 rebuildTriggers**：组件以 def 对象为键缓存候选，
//   遍历途中重建会把「空列表」缓存进去，之后再也刷不出来（本次现场抓到的真 bug）。
watch(projectDir, async () => {
  void loadAgentSkills()
  await loadProjectFiles()
  rebuildTriggers()
})

/* ------------------------------------------------------------------
   composer 升级（2026-10-15）：`/` 命令面板 + `@` 文件提及 + 上下文 chips
   数据源与动作全在本文件组装，组件只认 config 注入的形状（见 composer.ts）。
   ------------------------------------------------------------------ */

/** 脚本库（ScriptRunner 插件 GET /api/scripts），供 `/` 面板的「脚本」分组。 */
const agentScripts = ref<Array<{ id?: string; name?: string; description?: string; runtime?: string }>>([])
/** 项目文件相对路径清单（`@` 提及候选），上限 MAX_FILES。 */
const projectFiles = ref<string[]>([])

async function loadScripts() {
  try {
    const data = await apiGet<{ items?: Array<{ id?: string; name?: string; description?: string; runtime?: string }> }>('/api/scripts')
    agentScripts.value = (data?.items ?? []).filter((s) => !!s.id)
  } catch {
    agentScripts.value = []
  }
}

/**
 * 递归收集项目文件相对路径。
 * 限深 4 层 + 跳过 node_modules/.git/dist 等噪声目录（规则在 composer.ts，与单测同源）——
 * 大项目全量递归会直接卡死 UI。单层失败只丢那一层，不让整棵树陪葬。
 *
 * ★ 后端 `GET /api/project/files` 返回的字段名是 **`files`**（实测响应体
 *   `{success, root, files:[{name,isDirectory,relativePath,size}]}`）——
 *   不是别的列表接口常用的 `items`。写错字段名不会报错，只会让 `@` 面板永远空着。
 */
async function loadProjectFiles() {
  projectFiles.value = []
  if (!projectDir.value) return
  /* 收集规则（BFS / 限深 / 排除噪声）都在 composer.ts 的 collectProjectFiles，
     与单测同源；这里只负责把「取一层目录」接到 HTTP 上。 */
  projectFiles.value = await collectProjectFiles(async (rel) => {
    const data = await apiGet<{
      files?: Array<{ name?: string; isDirectory?: boolean }>
      items?: Array<{ name?: string; isDirectory?: boolean }>
    }>(withQuery('/api/project/files', { path: rel || undefined }))
    return (data?.files ?? data?.items ?? []).map((it) => ({
      name: it.name ?? '',
      isDirectory: !!it.isDirectory,
    }))
  })
}

/** `/` 面板的数据源（getter 形式，每次打开面板取最新值）。 */
function composerSources() {
  return {
    tools: () => agentTools.value,
    skills: () => agentSkills.value,
    scripts: () => agentScripts.value,
    agents: () => agents.value,
    models: () => models.value,
  }
}

/** 选中候选项后的动作（闸门1 P3：工具/技能/agent/模型挂上下文，不写进输入文本）。 */
function composerActions() {
  return {
    toggleTool: (name: string) => {
      const i = selectedToolNames.value.indexOf(name)
      if (i >= 0) selectedToolNames.value.splice(i, 1)
      else selectedToolNames.value = [...selectedToolNames.value, name]
    },
    toggleSkill: (id: string) => {
      const i = selectedSkillIds.value.indexOf(id)
      if (i >= 0) selectedSkillIds.value.splice(i, 1)
      else selectedSkillIds.value = [...selectedSkillIds.value, id]
    },
    selectAgent: (id: string) => {
      activeAgentId.value = id
      localStorage.setItem(LS_KEY_AGENT, id)
    },
    selectModel: (id: string) => {
      selectedModelId.value = id
      localStorage.setItem(LS_KEY_MODEL, id)
    },
    runBuiltin: (id: string) => {
      if (id === 'clear') startNewSession()
      else if (id === 'export') void exportCurrentSession()
      else ElMessage.info('可用命令：/clear 新会话 · /export 导出会话 · 输入 / 可浏览工具、技能、脚本')
    },
  }
}

/** 导出当前会话为 Markdown（组件已有 exportSession，这里只负责落盘与提示）。 */
async function exportCurrentSession() {
  const md = chatRef.value?.exportSession?.(sessionId.value)
  if (!md) {
    ElMessage.warning('当前会话没有可导出的内容')
    return
  }
  const blob = new Blob([md], { type: 'text/markdown;charset=utf-8' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `session-${sessionId.value}.md`
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 1000)
  ElMessage.success('已导出当前会话')
}

/**
 * 重建 `/` `@` 触发定义。
 * ★ 每次生成**新的 def 对象** —— 组件以 def 对象为 WeakMap 键缓存候选，
 *   换对象即让旧缓存失效（切项目后若沿用旧 def，`@` 会列着上一个项目的文件）。
 */
function rebuildTriggers() {
  agentConfig.triggers = [
    {
      char: '/',
      groups: async () => buildCommandGroups(composerSources()),
      onPick: (item: ComposerItem) => pickItem(item, composerActions()),
    },
    {
      char: '@',
      groups: async () => [
        {
          key: 'file',
          title: '项目文件',
          items: projectFiles.value.map((p) => ({ id: `file:${p}`, name: p, kind: 'file' as const })),
        },
      ],
      onPick: (item: ComposerItem) => pickItem(item, composerActions()),
    },
  ]
}

/**
 * 041 上下文图标行（输入框下方）+ 权限胶囊。
 * 与上面的 chips watchEffect 并列：chips 讲「已选了什么」，图标行讲「从哪儿去改」。
 */
watchEffect(() => {
  const cfg = buildContextBarConfig({
    projectDir: projectDir.value,
    agentName: activeAgent.value?.name ?? '',
    modelName: models.value.find((m) => m.chatModelId === selectedModelId.value)?.name ?? '',
    tools: agentTools.value,
    skills: agentSkills.value,
    agents: agents.value,
    models: models.value,
    selectedToolNames: selectedToolNames.value,
    selectedSkillIds: selectedSkillIds.value,
    activeAgentId: activeAgentId.value,
    selectedModelId: selectedModelId.value,
    accessMode: accessMode.value,
    openKey: ctxOpenKey.value,
    queries: ctxQueries.value,
    projectNames: knownProjects.value,
  })
  agentConfig.contextBar = {
    items: cfg.items,
    panels: cfg.panels,
    access: {
      modes: cfg.access.modes,
      value: cfg.access.value,
      onChange: (m) => setAccessMode(m as AccessMode),
    },
    onOpen: (k) => (ctxOpenKey.value = k),
    onClose: (k) => {
      if (k) ctxQueries.value = { ...ctxQueries.value, [k]: '' }
      else ctxOpenKey.value = null
    },
    onPick: (e) => onContextPick(e.key, e.id),
  }
})

/**
 * 041 待办**独立入口**（修订二）：数据走 config.todos，渲染在输入框下方底行最左。
 * 待办是常驻任务面板（要独立入口、看全部），与审批/询问（瞬时动作，走 config.ask）拆开。
 */
watchEffect(() => {
  const item = todosToAskItem(todos.value)
  agentConfig.todos = item
    ? {
        items: item.todos,
        openCount: (item.todos ?? []).filter((t) => !t.done).length,
        progress: (item.todos ?? []).filter((t) => t.done).length / Math.max(1, (item.todos ?? []).length),
      }
    : undefined
})

/**
 * 041 待你动作区（瞬时）：审批 / Agent 反问，紧贴输入框上沿（不进消息流）。
 * 当前后端无 approval 端点 → 无真实生产者，本键默认空；契约就绪等后端接入。
 */
watchEffect(() => {
  agentConfig.ask = buildAskConfig(askItems.value) ?? undefined
})

/** 切换权限档位：立刻生效于下一条消息（transport 每次发送取最新值），并持久化。 */
function setAccessMode(mode: AccessMode) {
  accessMode.value = mode
  localStorage.setItem(LS_KEY_ACCESS, mode)
  const meta = ACCESS_MODE_LABEL[mode] ?? mode
  ElMessage.success(`权限已切到「${meta}」，下一条消息生效`)
}

/** 图标行点选分派：单选类直接切，多选类取反（与 `/` 面板 pickItem 同语义）。 */
function onContextPick(key: string, id: string) {
  if (!id) return
  if (key === 'agent') activateAgent(id)
  else if (key === 'model') {
    selectedModelId.value = id
    localStorage.setItem(LS_KEY_MODEL, id)
  } else if (key === 'project') {
    /* 项目面板的特殊行：清除当前目录（旧 chips 的 × 撤掉后，能力在这里接住） */
    if (id === PROJECT_CLEAR_ID) onClearDirectory()
    else void onSelectDirectory(id)
  } else if (key === 'tools') {
    const i = selectedToolNames.value.indexOf(id)
    if (i >= 0) selectedToolNames.value.splice(i, 1)
    else selectedToolNames.value = [...selectedToolNames.value, id]
  } else if (key === 'skills') {
    const i = selectedSkillIds.value.indexOf(id)
    if (i >= 0) selectedSkillIds.value.splice(i, 1)
    else selectedSkillIds.value = [...selectedSkillIds.value, id]
  }
}

/** 权限档位中文名（提示用；权威定义在 composerContext.ACCESS_MODES）。 */
const ACCESS_MODE_LABEL: Record<AccessMode, string> = {
  readonly: '只读',
  standard: '标准',
  full: '完全访问',
}

/** 加载待办（输入框上方「待办」卡的数据源；失败静默 —— 待办不是主链路）。 */
async function loadTodos() {
  try {
    /* apiGet 已把响应体的 data 部分剥出来（见 http.ts request），
       所以这里是 { items, total, ... }，不能再取一层 .data —— 多取一层恒为 undefined，
       待办卡就永远不渲染（实测踩过：后端明明返回 2 条，前端 items 却是空）。 */
    const data = await apiGet<{ items?: TodoDtoLike[] }>('/api/todos?page=1&pageSize=20')
    todos.value = data?.items ?? []
  } catch {
    todos.value = []
  }
}

/** 记住本机用过的项目目录，供项目面板一点即选（不写后端，纯本地便利）。 */
const LS_KEY_PROJECTS = 'forgeself-agent-known-projects'
function rememberProject(dir: string) {
  if (!dir) return
  const next = [dir, ...knownProjects.value.filter((p) => p !== dir)].slice(0, 8)
  knownProjects.value = next
  try {
    localStorage.setItem(LS_KEY_PROJECTS, JSON.stringify(next))
  } catch {
    /* 隐私模式/超配额：面板退回「无已登记项目」，不阻断 */
  }
}
function onClearDirectory() {
  projectDir.value = ''
  localStorage.removeItem(LS_KEY_PROJECT)
}

/** 选择项目工作目录：写后端 + 更新本地状态 + 持久化。 */
async function onSelectDirectory(path: string) {
  try {
    const data = await apiPost<{ root?: string }>('/api/project/directory', { path })
    projectDir.value = data?.root ?? path
    if (projectDir.value) {
      localStorage.setItem(LS_KEY_PROJECT, projectDir.value)
      rememberProject(projectDir.value)
    }
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
  /* 状态矩阵「加载中」（041）：切会话 / 首屏拉历史时消息区会短暂空白，
     没有指示就会被读成「卡住了」。切换与首屏都走这一个入口，
     所以 loading 只在它这里开关，finished 一定在 finally 里关掉。 */
  agentConfig.loading = true
  try {
    const list = await apiGet<ChatMessage[]>(
      `/api/ai-agent/chat/history/${encodeURIComponent(sessionId.value)}?limit=50`
    )
    // 历史消息从后端 ToolCallsJson（FreeLoop 落库，031 方案A）解析工具轨迹，刷新后仍可见工具卡片
    const raw = (list ?? []).map((m, i) => ({
      ...m,
      id: m.id ?? `h-${i}`,
      toolEvents: parseToolEvents(m.toolCallsJson),
    }))
    // v1.8.0：组件无工具步骤卡片，后端 FreeLoop 落库的 content 为空的 assistant 工具步骤消息
    // 若直接灌入组件会渲染成「头像+空气泡」。过滤之，并把工具轨迹并入该轮下一条有内容的
    // assistant 回复（左栏工具调用统计不丢）；末尾残留轨迹并入最后一条可见助手消息。
    const visible: ChatMessage[] = []
    let pendingToolEvents: ToolEvent[] = []
    for (const m of raw) {
      const hasContent = (m.content ?? '').trim().length > 0
      if (m.role === 'assistant' && !hasContent) {
        pendingToolEvents.push(...(m.toolEvents ?? []))
        continue
      }
      if (m.role === 'assistant' && pendingToolEvents.length > 0) {
        m.toolEvents = [...pendingToolEvents, ...(m.toolEvents ?? [])]
        pendingToolEvents = []
      }
      if (hasContent) visible.push(m)
    }
    if (pendingToolEvents.length > 0) {
      for (let i = visible.length - 1; i >= 0; i--) {
        if (visible[i].role === 'assistant') {
          visible[i].toolEvents = [...(visible[i].toolEvents ?? []), ...pendingToolEvents]
          break
        }
      }
    }
    messages.value = visible
  } catch {
    // 新会话尚无历史，视为空即可
    messages.value = []
  } finally {
    agentConfig.loading = false
  }
}

/**
 * 拉取历史会话列表（T2：后端按 SessionId 聚合，返回标题/计数/时间）。
 * 显式传 active：agent 页只展示未归档会话，已归档会话在会话管理页看（后端默认值也是 active，此处写明意图）。
 */
async function loadSessions() {
  try {
    // B9-3：plan:{runId} 规划 scratch 会话不进聊天会话列表（后端已过滤，前端兜底）
    // 2026-10-14 修复：后端返回字段是 sessionId（SessionSummaryModel），原 s.id 为 undefined →
    // undefined.startsWith 抛 TypeError → catch 置空 → 历史会话恒 0（dev 验收发现，非本批引入）。
    sessions.value = ((await fetchSessions('active')) ?? []).filter((s) => !s.sessionId.startsWith('plan:'))
  } catch {
    // 列表拉取失败不阻断对话，留空即可
    sessions.value = []
  }
}

/** 切换历史会话：以全 id 回传（杜绝 T3 前缀 bug），加载其历史消息并灌入组件。 */
async function selectSession(id: string) {
  sessionId.value = id
  localStorage.setItem(LS_KEY_SESSION, id)
  await loadHistory()
  // v1.8.0：组件 hydrate 灌入历史（内部会 emit state-changed → 左栏统计/列表刷新）
  chatRef.value?.hydrate(messages.value)
}

/**
 * 归档确认弹窗（注入给 archiveSessionWithConfirm 的确认动作）。
 *
 * 归档虽是软标记（不删消息、可取消），但会让会话立刻从列表消失，属「可见状态突变」，
 * 故先弹确认再执行（用户反馈 2026-09-22：原来点一下就直接归档了）。
 * 文案必须讲清「消息保留 + 可在会话管理页取消归档」，避免被误读成删除。
 * 取消 / 关闭弹窗统一返回 false，由编排层短路为 cancelled——**不发起任何请求**。
 */
async function confirmArchive(sessionIdFull: string, title?: string): Promise<boolean> {
  try {
    await ElMessageBox.confirm(
      `归档后「${title?.trim() || sessionIdFull}」将从历史会话列表移除。消息会完整保留，` +
        '可在会话管理页取消归档。',
      '归档确认',
      { type: 'warning', confirmButtonText: '归档', cancelButtonText: '取消' },
    )
    return true
  } catch {
    return false // 用户取消 / 关闭弹窗
  }
}

/**
 * 归档历史会话：软标记（不删消息），归档后该会话即从 agent 页列表消失。
 * 若归档的是当前会话，则开新会话——已归档会话不应继续作为 agent 页的工作会话。
 * 取消归档则原地返回，既不请求也不刷新（列表本就没变）。
 */
async function onArchiveSession(id: string) {
  if (!id) return
  const target = sessions.value.find((s) => s.sessionId === id)
  const res = await archiveSessionWithConfirm(id, confirmArchive, target?.title)

  if (res.outcome === 'cancelled') return
  if (res.outcome === 'archived') {
    ElMessage.success('会话已归档，可在会话管理页查看')
    if (id === sessionId.value) startNewSession()
  } else {
    ElMessage.error(`归档失败：${res.error ?? '未知错误'}`)
  }
  // 无论成功或失败都按后端真实状态刷新列表（失败时也回到真实状态，不留幻象）
  await loadSessions()
}

/** 解析后端 ToolCallsJson（camelCase: name/args/result/success）为 ToolEvent[]，供历史消息渲染工具卡片。 */
function parseToolEvents(json?: string): ToolEvent[] {
  if (!json) return []
  try {
    const arr = JSON.parse(json) as unknown
    if (!Array.isArray(arr)) return []
    return arr
      .filter((t): t is Record<string, unknown> => !!t && typeof t === 'object')
      .map((t) => ({
        name: typeof t.name === 'string' ? t.name : undefined,
        args: typeof t.args === 'string' ? t.args : undefined,
        result: typeof t.result === 'string' ? t.result : undefined,
        success: typeof t.success === 'boolean' ? t.success : undefined,
      }))
  } catch {
    return []
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
  // v1.8.0：清空组件消息区（内部 emit state-changed → 左栏统计归零）
  chatRef.value?.hydrate([])
  // 新会话尚无消息，列表刷新后旧会话仍保留；此调用主要保证后续首条消息落库后能及时出现在列表
  void loadSessions()
}

onMounted(async () => {
  sessionId.value = ensureSessionId()
  await loadMeta()
  await loadAgents()
  await loadAgentTools()
  await loadAgentSkills()
  // composer 升级：脚本库（`/` 面板「脚本」分组）
  await loadScripts()
  // 恢复上次选定的工作目录（若有），同步到后端供文件工具使用
  const savedDir = localStorage.getItem(LS_KEY_PROJECT)
  if (savedDir) {
    projectDir.value = savedDir
    void onSelectDirectory(savedDir)
  }
  await loadHistory()
  await loadSessions()
  // composer 升级：目录恢复完成后再收文件（`@` 提及）+ 建触发定义（此时工具/技能/脚本/文件都已就位）
  await loadProjectFiles()
  rebuildTriggers()
  // 041：恢复权限档位（默认 standard）与已登记项目；再拉待办供输入框上方「待办」卡渲染
  const savedAccess = localStorage.getItem(LS_KEY_ACCESS) as AccessMode | null
  if (savedAccess === 'readonly' || savedAccess === 'standard' || savedAccess === 'full') {
    accessMode.value = savedAccess
  }
  try {
    const known = JSON.parse(localStorage.getItem(LS_KEY_PROJECTS) || '[]')
    if (Array.isArray(known)) knownProjects.value = known.filter((p) => typeof p === 'string')
  } catch {
    knownProjects.value = []
  }
  if (projectDir.value) rememberProject(projectDir.value)
  void loadTodos()
  // v1.8.0：初始会话历史灌入组件（hydrate 内部 emit state-changed → 左栏统计初始化）
  chatRef.value?.hydrate(messages.value)
})
</script>

<style scoped>
/* ---- 中栏顶部条（v1.8.0 起由本页承载；样式迁移自 ChatPanel.vue，设计原型 44px） ---- */
.chat {
  display: flex;
  flex-direction: column;
}

.chat__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 44px;
  flex-shrink: 0;
  padding: 0 16px;
  background: var(--el-bg-color);
  border-bottom: 1px solid var(--el-border-color);
}

.chat__bar-left,
.chat__bar-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.chat__title {
  font-size: var(--el-font-size-base, 14px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-primary);
}

/* 版本徽标：主色淡底胶囊 */
.chat__version {
  display: inline-block;
  margin-left: 8px;
  padding: 1px 6px;
  font-size: var(--el-font-size-extra-small, 12px);
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light);
  border-radius: 999px;
  vertical-align: middle;
}

.chat__status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-success);
}

.chat__dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: var(--el-color-success);
}

.chat__dot--busy {
  background: var(--el-color-warning);
}

/* 左右栏折叠按钮 */
.chat__fold {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  background: transparent;
  color: var(--el-text-color-secondary);
  cursor: pointer;
}

.chat__fold:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}

/* Fold/Expand 图标尺寸钳制（EP 图标 :size 在共享桥下不转 width/height，须显式给宽高） */
.chat__fold-ico {
  width: 14px;
  height: 14px;
}

/* A3：自研按钮键盘聚焦可见焦点环（ElButton 由 EP 自带聚焦样式） */
.chat__fold:focus-visible {
  outline: 2px solid var(--el-color-primary);
  outline-offset: 1px;
}
</style>
