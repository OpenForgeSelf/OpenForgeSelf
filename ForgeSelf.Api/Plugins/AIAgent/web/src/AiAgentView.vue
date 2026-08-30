<template>
  <div class="agent">
    <!-- 左栏：AI 上下文（MCP 工具 / 技能 / 提示指令 / 记忆） -->
    <ContextPanel />

    <!-- 中栏：完整聊天区 -->
    <ChatPanel
      :messages="messages"
      :models="models"
      :selected-model-id="selectedModelId"
      :sending="sending"
      :error="error"
      :token-text="tokenText"
      :version="version"
      @update:selected-model-id="onModelChange"
      @send="sendMessage"
    />

    <!-- 右栏：会话与统计 -->
    <SessionPanel
      :session-id="sessionId"
      :message-count="messageCount"
      :tool-call-count="toolCallCount"
      :token-text="tokenText"
      @new-session="startNewSession"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * AIAgent 插件自带界面（spec 010 试点）。
 *
 * 形态：三栏完整聊天界面，对齐设计原型 `forgeself-design/pages/ai-agent.html`
 * （左 220px 上下文 / 中 flex-1 聊天 / 右 260px 会话统计）。
 *
 * 设计约束（重要）：
 * - **不使用 Element Plus 组件**。宿主的 EP 组件由 unplugin-vue-components 在编译期局部注册，
 *   而本文件是独立构建的预编译产物，宿主打包器不会处理它，写 <ElXxx> 运行时必然解析失败。
 *   因此全部用原生 HTML + CSS，仅复用 EP 的 CSS 变量保持视觉一致（变量均带兜底值）。
 * - 不引用宿主的 `@/stores`、`@/services`、`@/components`（`@/` 是宿主别名，插件无从解析），
 *   数据一律通过 HTTP 直连后端（见 http.ts）。
 * - vue 为外部依赖，经宿主页面的 import map 解析到宿主同一份实例，避免 Vue 双实例。
 *
 * 状态说明：消息与会话状态提升到本组件持有，是为了让右栏「会话统计」显示真实数据，
 * 避免出现两处独立状态导致统计与界面不一致。
 */
import { computed, onMounted, ref } from 'vue'
import { apiGet, apiPost } from './http'
import type { AIModel, ChatMessage } from './types'
import ContextPanel from './components/ContextPanel.vue'
import ChatPanel from './components/ChatPanel.vue'
import SessionPanel from './components/SessionPanel.vue'

/** 插件 id，与 plugin.json 的 Id 对齐。 */
const PLUGIN_ID = 'ai-agent'
/** 选中模型的本地持久化键（与宿主内置页面一致，便于共享选择）。 */
const LS_KEY_MODEL = 'forgeself-agent-current-model'
/** 当前会话 id 的本地持久化键。 */
const LS_KEY_SESSION = 'forgeself-agent-session-id'

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

/** 消息条数（真实统计）。 */
const messageCount = computed(() => messages.value.length)
/** 工具调用次数（真实统计：累加各条消息的工具徽标数）。 */
const toolCallCount = computed(() =>
  messages.value.reduce((sum, m) => sum + (m.toolCalls?.length ?? 0), 0)
)
/** token 用量文案；后端暂无统计接口时如实显示占位符。 */
const tokenText = computed(() => '—')

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
    if (saved && flat.some((m) => String(m.id) === saved)) {
      selectedModelId.value = saved
    } else if (flat.length > 0) {
      selectedModelId.value = String(flat[0]?.id ?? '')
      localStorage.setItem(LS_KEY_MODEL, selectedModelId.value)
    }
  } catch {
    models.value = []
  }
}

/** 加载当前会话的历史消息。 */
async function loadHistory() {
  if (!sessionId.value) return
  try {
    const list = await apiGet<ChatMessage[]>(
      `/api/chat/history/${encodeURIComponent(sessionId.value)}?limit=50`
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

/** 发送消息：先本地追加用户消息，再请求后端并把回复追加到列表。 */
async function sendMessage(text: string) {
  if (sending.value) return
  error.value = ''
  sending.value = true

  // 乐观追加用户消息，保证界面即时反馈
  messages.value.push({ id: `u-${Date.now()}`, role: 'user', content: text })

  try {
    // stream 传 false：取非流式完整回复，实现最简且不依赖 SSE。
    // chatModelId（形如 `provider:upstreamId`，如 `default:qwythos-9b-v2`）
    // 传给后端用于锁定本次对话使用的模型，与 UI 下拉选择保持一致。
    // 若未选择或模型列表为空，后端回落到默认模型（行为不变）。
    const reply = await apiPost<ChatMessage>('/api/chat', {
      sessionId: sessionId.value,
      message: text,
      stream: false,
      chatModelId: selectedModelId.value || undefined,
    })
    if (reply) {
      messages.value.push({
        id: reply.id ?? `a-${Date.now()}`,
        role: reply.role || 'assistant',
        content: reply.content ?? '',
        createTime: reply.createTime,
      })
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    sending.value = false
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
  await loadHistory()
})
</script>

<style scoped>
/* 三栏布局：高度撑满宿主内容区，左右定宽、中间自适应（对齐设计原型） */
.agent {
  display: flex;
  height: 100%;
  min-height: 0;
  overflow: hidden;
  background: var(--el-bg-color-page, #0f1115);
  color: var(--el-text-color-primary, #e5eaf3);
}
</style>
