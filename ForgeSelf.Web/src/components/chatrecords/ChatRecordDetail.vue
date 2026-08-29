<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ChatTurn } from '@/types/chatRecords'
import { parseJsonSequence } from '@/utils/jsonSequence'
import TruncatedContent from './TruncatedContent.vue'
import JsonTreeView from './JsonTreeView.vue'

const props = defineProps<{
  record: ChatTurn
}>()

// ===== 通用类型：把三种风格的请求结构归一化为「消息 + 内容块」 =====
type ContentType = 'text' | 'image_url' | 'tool_use' | 'tool_result' | 'thinking' | 'raw'

interface ContentBlock {
  type: ContentType
  text?: string
  imageUrl?: string
  imageNote?: string
  toolName?: string
  toolArgs?: string
  toolCallId?: string
}

interface MessageBlock {
  role: string
  contents: ContentBlock[]
  fromResponse?: boolean
}

interface UsageInfo {
  inputTokens?: number
  outputTokens?: number
  totalTokens?: number
}

const activeTab = ref<'conversation' | 'raw'>('conversation')
const rawExpanded = ref(true)// 响应体可能为：单对象（completion/error）/ 流式分片拼接的数组 / 空。
// 统一规整为「对象数组」交由下方 normalizer 处理。
const responseChunks = computed<Record<string, unknown>[]>(() => {
  const rb = props.record.responseBody
  if (Array.isArray(rb)) return rb as Record<string, unknown>[]
  if (rb && typeof rb === 'object') return [rb as Record<string, unknown>]
  if (typeof rb === 'string') return parseJsonSequence(rb) as Record<string, unknown>[]
  return []
})

const requestBody = computed<Record<string, unknown>>(() => {
  const rb = props.record.requestBody
  if (typeof rb === 'string') {
    try {
      return JSON.parse(rb) as Record<string, unknown>
    } catch {
      return {}
    }
  }
  return (rb || {}) as Record<string, unknown>
})

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`
  if (ms < 60000) return `${(ms / 1000).toFixed(2)}s`
  return `${(ms / 60000).toFixed(2)}分钟`
}

// ===== 把请求归一化为消息块 =====
const messages = computed<MessageBlock[]>(() => {
  const style = props.record.style
  const body = requestBody.value

  if (style === 'OpenAI_Chat') {
    const raw = body['messages']
    if (!Array.isArray(raw)) return []
    return raw.map((m) => normalizeOpenAIMessage(m as Record<string, unknown>))
  }
  if (style === 'Anthropic_Messages') {
    const blocks: MessageBlock[] = []
    // 顶层 system 也当作一条 system 消息展示
    if (body['system'] !== undefined) {
      blocks.push({ role: 'system', contents: [{ type: 'text', text: typeof body['system'] === 'string' ? (body['system'] as string) : JSON.stringify(body['system']) }] })
    }
    const raw = body['messages']
    if (Array.isArray(raw)) {
      for (const m of raw as Record<string, unknown>[]) {
        blocks.push(normalizeAnthropicMessage(m))
      }
    }
    return blocks
  }
  if (style === 'OpenAI_Responses') {
    const raw = body['input']
    if (!Array.isArray(raw)) return []
    return raw.map((it) => normalizeResponsesItem(it as Record<string, unknown>))
  }
  return []
})

function normalizeOpenAIMessage(m: Record<string, unknown>): MessageBlock {
  const role = (m['role'] as string) || 'unknown'
  const contents: ContentBlock[] = []
  const content = m['content']
  if (typeof content === 'string') {
    contents.push({ type: 'text', text: content })
  } else if (Array.isArray(content)) {
    for (const part of content as Record<string, unknown>[]) {
      const t = part['type'] as string
      if (t === 'text') contents.push({ type: 'text', text: (part['text'] as string) || '' })
      else if (t === 'image_url') contents.push({ type: 'image_url', imageUrl: ((part['image_url'] as Record<string, unknown>)?.['url'] as string) || '' })
      else contents.push({ type: 'raw', text: JSON.stringify(part, null, 2) })
    }
  }
  // tool_calls：OpenAI Chat 中 assistant 消息内联的函数调用
  const toolCalls = m['tool_calls']
  if (Array.isArray(toolCalls)) {
    for (const tc of toolCalls as Record<string, unknown>[]) {
      const fn = (tc['function'] as Record<string, unknown>) || {}
      contents.push({
        type: 'tool_use',
        toolName: (fn['name'] as string) || 'unknown',
        toolArgs: JSON.stringify(fn['arguments'] ?? {}, null, 2),
        toolCallId: tc['id'] as string | undefined
      })
    }
  }
  // tool 角色消息：携带工具执行结果
  if (role === 'tool') {
    contents.push({ type: 'tool_result', text: typeof content === 'string' ? content : JSON.stringify(content), toolCallId: m['tool_call_id'] as string | undefined })
  }
  return { role, contents }
}

function normalizeAnthropicMessage(m: Record<string, unknown>): MessageBlock {
  const role = (m['role'] as string) || 'unknown'
  const contents: ContentBlock[] = []
  const content = m['content']
  if (typeof content === 'string') {
    contents.push({ type: 'text', text: content })
  } else if (Array.isArray(content)) {
    for (const block of content as Record<string, unknown>[]) {
      const t = block['type'] as string
      if (t === 'text') contents.push({ type: 'text', text: (block['text'] as string) || '' })
      else if (t === 'thinking') contents.push({ type: 'thinking', text: (block['thinking'] as string) || '' })
      else if (t === 'tool_use') contents.push({ type: 'tool_use', toolName: (block['name'] as string) || 'unknown', toolArgs: JSON.stringify(block['input'] ?? {}, null, 2), toolCallId: block['id'] as string | undefined })
      else if (t === 'tool_result') contents.push({ type: 'tool_result', text: anthropicToolResultText(block['content']), toolCallId: block['tool_use_id'] as string | undefined })
      else if (t === 'image') {
        const src = (block['source'] as Record<string, unknown>) || {}
        if (src['type'] === 'url') contents.push({ type: 'image_url', imageUrl: (src['url'] as string) || '' })
        else contents.push({ type: 'image_url', imageNote: '图片(base64)', imageUrl: '' })
      } else contents.push({ type: 'raw', text: JSON.stringify(block, null, 2) })
    }
  }
  return { role, contents }
}

function anthropicToolResultText(content: unknown): string {
  if (typeof content === 'string') return content
  if (Array.isArray(content)) {
    return (content as Record<string, unknown>[])
      .map((b) => (b['type'] === 'text' ? (b['text'] as string) : JSON.stringify(b)))
      .join('\n')
  }
  return JSON.stringify(content)
}

function normalizeResponsesItem(it: Record<string, unknown>): MessageBlock {
  const type = (it['type'] as string) || 'unknown'
  if (type === 'message') {
    const role = (it['role'] as string) || 'unknown'
    const contents: ContentBlock[] = []
    const content = it['content']
    if (Array.isArray(content)) {
      for (const part of content as Record<string, unknown>[]) {
        const t = part['type'] as string
        if (t === 'input_text' || t === 'output_text' || t === 'refusal') contents.push({ type: 'text', text: (part['text'] as string) || '' })
        else if (t === 'input_image') {
          const img = (part['image_url'] as Record<string, unknown>) || {}
          contents.push({ type: 'image_url', imageUrl: (img['url'] as string) || '' })
        } else contents.push({ type: 'raw', text: JSON.stringify(part, null, 2) })
      }
    }
    return { role, contents }
  }
  if (type === 'function_call') {
    return {
      role: 'assistant',
      contents: [{
        type: 'tool_use',
        toolName: (it['name'] as string) || 'unknown',
        toolArgs: typeof it['arguments'] === 'string' ? (it['arguments'] as string) : JSON.stringify(it['arguments'] ?? {}, null, 2),
        toolCallId: it['call_id'] as string | undefined
      }]
    }
  }
  if (type === 'function_call_output') {
    return {
      role: 'tool',
      contents: [{ type: 'tool_result', text: typeof it['output'] === 'string' ? (it['output'] as string) : JSON.stringify(it['output']), toolCallId: it['call_id'] as string | undefined }]
    }
  }
  return { role: type, contents: [{ type: 'raw', text: JSON.stringify(it, null, 2) }] }
}

// ===== 响应归一化：兼容 单对象 / 流式分片拼接 / 错误对象 三种形态 =====
interface NormalizedResponse {
  isError: boolean
  errorText?: string
  assistantText: string
  reasoningTexts: string[]
  finishReason: string | null
  usage: UsageInfo | null
  chunkCount: number
}

function extractUsage(u: unknown): UsageInfo | null {
  if (!u || typeof u !== 'object') return null
  const o = u as Record<string, unknown>
  const info: UsageInfo = {}
  // OpenAI / DeepSeek
  if (typeof o['prompt_tokens'] === 'number') info.inputTokens = o['prompt_tokens'] as number
  if (typeof o['completion_tokens'] === 'number') info.outputTokens = o['completion_tokens'] as number
  if (typeof o['total_tokens'] === 'number') info.totalTokens = o['total_tokens'] as number
  // Anthropic
  if (typeof o['input_tokens'] === 'number') info.inputTokens = o['input_tokens'] as number
  if (typeof o['output_tokens'] === 'number') info.outputTokens = o['output_tokens'] as number
  if (info.inputTokens === undefined && info.outputTokens === undefined && info.totalTokens === undefined) return null
  return info
}

function normalizeResponse(chunks: Record<string, unknown>[]): NormalizedResponse {
  const base: NormalizedResponse = {
    isError: false,
    assistantText: '',
    reasoningTexts: [],
    finishReason: null,
    usage: null,
    chunkCount: chunks.length
  }
  if (chunks.length === 0) return base

  // 单对象：completion 或 error
  if (chunks.length === 1) {
    const o = chunks[0]
    if (o && o['error']) {
      const err = o['error']
      return { ...base, isError: true, errorText: typeof err === 'string' ? err : JSON.stringify(err) }
    }
    const choice = Array.isArray(o['choices']) ? (o['choices'] as Record<string, unknown>[])[0] : undefined
    const msg = choice && (choice['message'] as Record<string, unknown> | undefined)
    const content = msg ? msg['content'] : undefined
    const text = typeof content === 'string' ? content : content && typeof content === 'object' ? JSON.stringify(content) : ''
    const fr = (choice && (choice['finish_reason'] as string)) || (o['stop_reason'] as string) || null
    const reasoning: string[] = []
    if (msg) {
      if (typeof msg['reasoning_content'] === 'string' && msg['reasoning_content']) reasoning.push(msg['reasoning_content'] as string)
      if (typeof msg['reasoning'] === 'string' && msg['reasoning']) reasoning.push(msg['reasoning'] as string)
    }
    return { ...base, assistantText: text || '', reasoningTexts: reasoning, finishReason: fr, usage: extractUsage(o['usage']) }
  }

  // 多对象：流式分片（chat.completion.chunk）逐个合并
  let text = ''
  let reasoning = ''
  let finishReason: string | null = null
  let usage: UsageInfo | null = null
  for (const chunk of chunks) {
    if (!chunk || typeof chunk !== 'object') continue
    if (chunk['error']) {
      const err = chunk['error']
      return { ...base, isError: true, errorText: typeof err === 'string' ? err : JSON.stringify(err) }
    }
    const choice = Array.isArray(chunk['choices']) ? (chunk['choices'] as Record<string, unknown>[])[0] : undefined
    const delta = choice && (choice['delta'] as Record<string, unknown> | undefined)
    if (delta) {
      if (typeof delta['content'] === 'string') text += delta['content']
      if (typeof delta['reasoning_content'] === 'string') reasoning += delta['reasoning_content']
    }
    if (choice && choice['finish_reason']) finishReason = choice['finish_reason'] as string
    const u = chunk['usage'] || (delta && delta['usage'])
    if (u) usage = extractUsage(u)
  }
  return {
    ...base,
    assistantText: text,
    reasoningTexts: reasoning.trim() ? [reasoning] : [],
    finishReason,
    usage
  }
}

const normalizedResponse = computed(() => normalizeResponse(responseChunks.value))
const finishReason = computed(() => normalizedResponse.value.finishReason)
const usage = computed(() => normalizedResponse.value.usage)
const reasoningTexts = computed(() => normalizedResponse.value.reasoningTexts)
const assistantText = computed(() => normalizedResponse.value.assistantText)
const errorText = computed(() => normalizedResponse.value.errorText ?? '')
const hasAssistantText = computed(() => assistantText.value.trim().length > 0)

// ===== 对话流：请求消息（按原序）+ 响应 assistant 终条，合成连续对话 =====
const conversationFlow = computed<MessageBlock[]>(() => {
  const flow: MessageBlock[] = [...messages.value]
  const nr = normalizedResponse.value
  if (nr.isError) {
    flow.push({ role: 'assistant', fromResponse: true, contents: [{ type: 'raw', text: '⚠ ' + (errorText.value || '错误响应') }] })
  } else if (hasAssistantText.value || reasoningTexts.value.length > 0) {
    const contents: ContentBlock[] = []
    for (const rt of reasoningTexts.value) contents.push({ type: 'thinking', text: rt })
    if (hasAssistantText.value) contents.push({ type: 'text', text: assistantText.value })
    flow.push({ role: 'assistant', fromResponse: true, contents })
  }
  return flow
})

function avatarFor(role: string): { icon: string; cls: string; label: string } {
  switch (role) {
    case 'user': return { icon: '👤', cls: 'avatar-user', label: '用户' }
    case 'assistant': return { icon: '🤖', cls: 'avatar-ai', label: 'AI' }
    case 'tool': return { icon: '🔧', cls: 'avatar-tool', label: '工具' }
    case 'system': return { icon: '⚙', cls: 'avatar-system', label: '系统' }
    default: return { icon: '💬', cls: 'avatar-default', label: role }
  }
}

function badgeForType(type: ContentType): string {
  switch (type) {
    case 'text': return 'content-text'
    case 'image_url': return 'content-image'
    case 'tool_use': return 'content-tool'
    case 'tool_result': return 'content-result'
    case 'thinking': return 'content-thinking'
    default: return 'content-raw'
  }
}

// 系统提示词内容：拼接文本型内容块，供对话视图展示（超长可展开/收起）
function systemContent(msg: MessageBlock): string {
  return msg.contents
    .filter((c) => c.type === 'text' && c.text)
    .map((c) => c.text as string)
    .join('\n')
}
</script>

<template>
  <div class="chat-record-detail">
    <!-- 头部元信息 -->
    <div class="detail-head">
      <div class="head-title">
        聊天记录详情 <span class="head-id">#{{ record.id }}</span>
      </div>
      <div class="head-badges">
        <span class="head-badge" :class="record.responseStatus >= 400 ? 'badge-error' : 'badge-ok'">
          HTTP {{ record.responseStatus }}
        </span>
        <span class="head-badge badge-style">{{ record.style }}</span>
      </div>
    </div>
    <div class="head-meta">
      <span><b>Model</b> {{ record.model }}</span>
      <span><b>Session</b> {{ record.sessionKey.slice(0, 12) }}…</span>
      <span><b>耗时</b> {{ formatDuration(record.durationMs) }}</span>
      <span><b>消息</b> {{ record.messageCount }}</span>
      <span><b>工具</b> {{ record.toolCallCount }}</span>
    </div>

    <!-- 视图切换 -->
    <div class="tab-bar">
      <button class="tab-btn" :class="{ active: activeTab === 'conversation' }" type="button" @click="activeTab = 'conversation'">
        💬 对话视图
      </button>
      <button class="tab-btn" :class="{ active: activeTab === 'raw' }" type="button" @click="activeTab = 'raw'">
        📄 原始数据
      </button>
    </div>

    <!-- 对话视图：按角色气泡 -->
    <div v-show="activeTab === 'conversation'" class="conversation">
      <div v-if="conversationFlow.length === 0" class="empty-hint">无结构化对话内容（可切换到「原始数据」查看）</div>

      <template v-for="(msg, idx) in conversationFlow" :key="idx">
        <!-- system：居中灰条 + 提示词内容（超长可展开/收起） -->
        <div v-if="msg.role === 'system'" class="sys-block">
          <div class="sys-bar">
            <span class="sys-icon">⚙</span> 系统提示
          </div>
          <TruncatedContent :content="systemContent(msg)" />
        </div>

        <!-- 气泡行 -->
        <div v-else class="bubble-row" :class="msg.role === 'user' ? 'row-user' : 'row-other'">
          <div class="avatar" :class="avatarFor(msg.role).cls">
            {{ avatarFor(msg.role).icon }}
          </div>
          <div class="bubble-col">
            <div class="bubble-role">{{ avatarFor(msg.role).label }}</div>
            <div class="bubble" :class="'bubble-' + msg.role">
              <template v-for="(c, ci) in msg.contents" :key="ci">
                <!-- 文本 / 思考 / 原始 -->
                <div v-if="c.type === 'text' || c.type === 'thinking' || c.type === 'raw'" class="content-item" :class="badgeForType(c.type)">
                  <span class="content-tag">{{ c.type === 'thinking' ? '💡 思考' : c.type === 'raw' ? '原始' : '文本' }}</span>
                  <TruncatedContent :content="c.text || ''" />
                </div>
                <!-- 图片 -->
                <div v-else-if="c.type === 'image_url'" class="content-item content-image">
                  <span class="content-tag">🖼 图片</span>
                  <img v-if="c.imageUrl" :src="c.imageUrl" class="content-img" alt="image" />
                  <span v-else class="image-note">{{ c.imageNote }}</span>
                </div>
                <!-- 工具调用（嵌 assistant 气泡内，可展开） -->
                <details v-else-if="c.type === 'tool_use'" class="tool-call">
                  <summary class="tool-call-summary">
                    🔧 工具调用 · <b>{{ c.toolName }}</b>
                    <span v-if="c.toolCallId" class="tool-call-id">{{ c.toolCallId.slice(0, 10) }}</span>
                  </summary>
                  <div class="tool-call-args">
                    <span class="info-label">参数</span>
                    <TruncatedContent :content="c.toolArgs || ''" />
                  </div>
                </details>
                <!-- 工具结果 -->
                <div v-else-if="c.type === 'tool_result'" class="content-item content-result">
                  <span class="content-tag">✅ 工具结果</span>
                  <TruncatedContent :content="c.text || ''" />
                </div>
              </template>
            </div>
            <!-- 响应终条底部 meta：usage / finish_reason -->
            <div v-if="msg.fromResponse" class="resp-meta">
              <span class="meta-item">finish: {{ finishReason ?? '无' }}</span>
              <span v-if="usage" class="meta-item">
                tokens: {{ usage.inputTokens ?? '—' }}↑ / {{ usage.outputTokens ?? '—' }}↓ / {{ usage.totalTokens ?? '—' }}∑
              </span>
              <span v-else class="meta-item meta-muted">未回传 usage</span>
            </div>
          </div>
        </div>
      </template>
    </div>

    <!-- 原始数据视图：基本信息 + 完整请求/响应 JSON -->
    <div v-show="activeTab === 'raw'" class="raw-view">
      <details :open="rawExpanded" class="raw-block">
        <summary class="raw-summary">基本信息</summary>
        <div class="info-grid">
          <div class="info-item"><span class="info-label">SessionKey</span><span class="info-value">{{ record.sessionKey }}</span></div>
          <div class="info-item"><span class="info-label">Style</span><span class="info-value">{{ record.style }}</span></div>
          <div class="info-item"><span class="info-label">Model</span><span class="info-value">{{ record.model }}</span></div>
          <div class="info-item"><span class="info-label">创建时间</span><span class="info-value">{{ record.createdTime }}</span></div>
          <div class="info-item"><span class="info-label">Duration</span><span class="info-value">{{ formatDuration(record.durationMs) }}</span></div>
          <div class="info-item"><span class="info-label">Temperature</span><span class="info-value">{{ record.temperature }}</span></div>
          <div v-if="record.maxTokens" class="info-item"><span class="info-label">MaxTokens</span><span class="info-value">{{ record.maxTokens }}</span></div>
          <div class="info-item"><span class="info-label">消息数</span><span class="info-value">{{ record.messageCount }}</span></div>
          <div class="info-item"><span class="info-label">工具调用数</span><span class="info-value">{{ record.toolCallCount }}</span></div>
          <div class="info-item"><span class="info-label">HasReasoning</span><span class="info-value">{{ record.hasReasoning ? '是' : '否' }}</span></div>
        </div>
      </details>

      <details :open="rawExpanded" class="raw-block">
        <summary class="raw-summary">完整请求体</summary>
        <JsonTreeView :data="requestBody" :default-expanded="false" />
      </details>

      <details :open="rawExpanded" class="raw-block">
        <summary class="raw-summary">完整响应体</summary>
        <JsonTreeView :data="responseChunks" :default-expanded="false" />
      </details>
    </div>
  </div>
</template>

<style scoped>
.chat-record-detail {
  display: flex;
  flex-direction: column;
  gap: 12px;
  height: 100%;
  min-height: 0;
}

/* 头部 */
.detail-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.head-title {
  font-size: 17px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.head-id {
  color: var(--el-color-primary);
}
.head-badges {
  display: flex;
  gap: 8px;
}
.head-badge {
  padding: 2px 10px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
}
.badge-ok { color: var(--el-color-success); background: color-mix(in srgb, var(--el-color-success) 12%, transparent); }
.badge-error { color: var(--el-color-danger); background: color-mix(in srgb, var(--el-color-danger) 12%, transparent); }
.badge-style { color: var(--el-color-primary); background: color-mix(in srgb, var(--el-color-primary) 12%, transparent); }

.head-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.head-meta b { color: var(--el-text-color-regular); font-weight: 600; margin-right: 4px; }

/* Tab */
.tab-bar {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--el-border-color);
  flex-shrink: 0;
}
.tab-btn {
  padding: 8px 14px;
  border: none;
  background: transparent;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: all 0.15s ease;
}
.tab-btn.active {
  color: var(--el-color-primary);
  border-bottom-color: var(--el-color-primary);
  font-weight: 600;
}

/* 对话区（可滚动） */
.conversation {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 4px 2px;
}

.empty-hint {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  text-align: center;
  padding: 24px 0;
}

/* system 居中灰条 + 提示词内容 */
.sys-block {
  align-self: center;
  max-width: 90%;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}
.sys-bar {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 14px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 12px;
}
.sys-icon { color: var(--el-color-info); }
/* 系统提示内容块：复用 content-item 视觉，但不抢眼 */
.sys-block .truncated-content {
  max-width: 100%;
  width: 100%;
}
.sys-block .content-text {
  background: var(--el-fill-color-light);
}

/* 气泡行 */
.bubble-row {
  display: flex;
  gap: 10px;
  align-items: flex-start;
}
.row-user { flex-direction: row-reverse; }
.row-other { flex-direction: row; }

.avatar {
  flex-shrink: 0;
  width: 38px;
  height: 38px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  color: var(--el-color-white);
}
.avatar-ai { background: var(--el-color-primary); }
.avatar-user { background: var(--el-color-success); }
.avatar-tool { background: var(--el-color-warning); }
.avatar-system { background: var(--el-color-info); }
.avatar-default { background: var(--el-text-color-secondary); }

.bubble-col {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-width: 78%;
}
.row-user .bubble-col { align-items: flex-end; }
.row-other .bubble-col { align-items: flex-start; }

.bubble-role {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.bubble {
  padding: 10px 14px;
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  line-height: 1.6;
  font-size: 14px;
  word-break: break-word;
}
.bubble-user {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  color: var(--el-text-color-primary);
  border-top-right-radius: 4px;
}
.bubble-assistant {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-top-left-radius: 4px;
}
.bubble-tool {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-light);
  border-top-left-radius: 4px;
}
.bubble-system, .bubble-unknown {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
}

/* 内容块 */
.content-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
}
.content-tool {
  background: var(--el-fill-color-light);
  border-color: var(--el-border-color-light);
}
.content-result {
  background: var(--el-fill-color-light);
  border-color: var(--el-border-color-light);
}
.content-thinking {
  background: var(--el-fill-color-light);
  border-color: var(--el-border-color-light);
}
.content-tag {
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}
.content-img { max-width: 200px; max-height: 150px; border-radius: 6px; border: 1px solid var(--el-border-color); }
.image-note { font-size: 12px; color: var(--el-text-color-secondary); }

/* 工具调用（可展开） */
.tool-call {
  border: 1px solid var(--el-border-color-light);
  background: var(--el-fill-color-light);
  border-radius: 8px;
  padding: 6px 10px;
}
.tool-call-summary {
  cursor: pointer;
  font-size: 13px;
  color: var(--el-text-color-primary);
  display: flex;
  align-items: center;
  gap: 6px;
}
.tool-call-id {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  font-family: 'Monaco', 'Menlo', monospace;
}
.tool-call-args {
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

/* 响应 meta */
.resp-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.meta-item {
  padding: 2px 8px;
  border-radius: 4px;
  background: var(--el-fill-color-light);
}
.meta-muted { color: var(--el-text-color-disabled); }

/* 原始数据 */
.raw-view {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.raw-block {
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
}
.raw-summary {
  padding: 10px 14px;
  background: var(--el-fill-color-light);
  cursor: pointer;
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.raw-summary:hover { background: var(--el-fill-color); }
.raw-block[open] .raw-summary { border-bottom: 1px solid var(--el-border-color); }
.raw-block > :not(summary) { padding: 12px 14px; }

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 10px;
}
.info-item { display: flex; flex-direction: column; gap: 4px; }
.info-label { font-size: 12px; font-weight: 500; color: var(--el-text-color-secondary); }
.info-value { font-size: 14px; color: var(--el-text-color-primary); word-break: break-all; }
</style>
