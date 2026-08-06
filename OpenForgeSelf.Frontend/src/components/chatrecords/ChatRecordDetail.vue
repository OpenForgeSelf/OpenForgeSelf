<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ChatRecord } from '@/types/chatRecords'
import TruncatedContent from './TruncatedContent.vue'
import StreamingText from './StreamingText.vue'

const props = defineProps<{
  record: ChatRecord
}>()

const basicInfoExpanded = ref(true)
const requestExpanded = ref(true)
const toolCallsExpanded = ref(true)
const aiReplyExpanded = ref(true)
const responseExpanded = ref(true)

const requestBodyStr = computed(() => JSON.stringify(props.record.requestBody, null, 2))
const responseBodyStr = computed(() => JSON.stringify(props.record.responseBody, null, 2))

const openAIChatFields = computed(() => {
  const body = props.record.requestBody
  if (!body) return null
  return {
    messages: body.messages,
    tools: body.tools,
    tool_choice: body.tool_choice,
    temperature: body.temperature,
    max_tokens: body.max_tokens,
    stream: body.stream,
    model: body.model
  }
})

const openAIResponsesFields = computed(() => {
  const body = props.record.requestBody
  if (!body) return null
  return {
    input: body.input,
    instructions: body.instructions,
    previous_response_id: body.previous_response_id,
    temperature: body.temperature,
    max_tokens: body.max_tokens
  }
})

const anthropicMessagesFields = computed(() => {
  const body = props.record.requestBody
  if (!body) return null
  return {
    model: body.model,
    messages: body.messages,
    system: body.system,
    max_tokens: body.max_tokens,
    temperature: body.temperature,
    thinking: body.thinking,
    tools: body.tools
  }
})

function toggleSection(section: 'basicInfo' | 'request' | 'toolCalls' | 'aiReply' | 'response') {
  switch (section) {
    case 'basicInfo':
      basicInfoExpanded.value = !basicInfoExpanded.value
      break
    case 'request':
      requestExpanded.value = !requestExpanded.value
      break
    case 'toolCalls':
      toolCallsExpanded.value = !toolCallsExpanded.value
      break
    case 'aiReply':
      aiReplyExpanded.value = !aiReplyExpanded.value
      break
    case 'response':
      responseExpanded.value = !responseExpanded.value
      break
  }
}

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`
  if (ms < 60000) return `${(ms / 1000).toFixed(2)}s`
  return `${(ms / 60000).toFixed(2)}分钟`
}

// 从原始响应体中抽取 LLM 回复的可读文本，用于「流式回放」
interface StreamContentBlock {
  text?: unknown
}
interface StreamChoice {
  message?: { content?: unknown }
}
interface StreamOutputItem {
  text?: string
  content?: unknown
}
interface RawResponseBody {
  choices?: StreamChoice[]
  output?: StreamOutputItem[]
  content?: unknown
  text?: string
  [key: string]: unknown
}

function extractTextFromContentBlocks(content: unknown): string {
  if (!Array.isArray(content)) return ''
  return content
    .map((block) => {
      if (block && typeof block === 'object' && 'text' in block) {
        const t = (block as StreamContentBlock).text
        return typeof t === 'string' ? t : ''
      }
      return ''
    })
    .join('')
}

function extractAssistantText(style: string, body: unknown): string {
  if (!body || typeof body !== 'object') return ''
  const b = body as RawResponseBody
  if (style === 'OpenAI_Chat') {
    const content = b.choices?.[0]?.message?.content
    if (typeof content === 'string') return content
    if (content && typeof content === 'object') return JSON.stringify(content)
    return ''
  }
  if (style === 'OpenAI_Responses') {
    if (typeof b.text === 'string') return b.text
    if (Array.isArray(b.output)) {
      return b.output
        .map((o) => (typeof o.text === 'string' ? o.text : extractTextFromContentBlocks(o.content)))
        .filter((t) => t.length > 0)
        .join('\n\n')
    }
    return ''
  }
  if (style === 'Anthropic_Messages') {
    return extractTextFromContentBlocks(b.content)
  }
  return ''
}

const assistantText = computed(() => extractAssistantText(props.record.style, props.record.responseBody))
const hasAssistantText = computed(() => assistantText.value.trim().length > 0)
</script>

<template>
  <div class="chat-record-detail">
    <h3 class="detail-title">聊天记录详情 #{{ record.id }}</h3>

    <div class="section">
      <div class="section-header" @click="toggleSection('basicInfo')">
        <span class="section-icon">{{ basicInfoExpanded ? '▼' : '▶' }}</span>
        <span class="section-title">基本信息</span>
      </div>
      <div v-show="basicInfoExpanded" class="section-content">
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">SessionId</span>
            <span class="info-value">{{ record.sessionId }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Style</span>
            <span class="info-value">{{ record.style }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Model</span>
            <span class="info-value">{{ record.model }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">创建时间</span>
            <span class="info-value">{{ record.createdTime }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Duration</span>
            <span class="info-value">{{ formatDuration(record.durationMs) }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Temperature</span>
            <span class="info-value">{{ record.temperature }}</span>
          </div>
          <div v-if="record.maxTokens" class="info-item">
            <span class="info-label">MaxTokens</span>
            <span class="info-value">{{ record.maxTokens }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">消息数</span>
            <span class="info-value">{{ record.messageCount }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">工具调用数</span>
            <span class="info-value">{{ record.toolCallCount }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">HasReasoning</span>
            <span class="info-value">{{ record.hasReasoning ? '是' : '否' }}</span>
          </div>
        </div>
      </div>
    </div>

    <div class="section">
      <div class="section-header" @click="toggleSection('request')">
        <span class="section-icon">{{ requestExpanded ? '▼' : '▶' }}</span>
        <span class="section-title">请求</span>
        <span class="section-meta">{{ record.requestMethod }} {{ record.requestPath }}</span>
      </div>
      <div v-show="requestExpanded" class="section-content">
        <div class="request-info">
          <div class="info-item">
            <span class="info-label">请求方法</span>
            <span class="info-value">{{ record.requestMethod }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">请求路径</span>
            <span class="info-value">{{ record.requestPath }}</span>
          </div>
        </div>

        <template v-if="record.style === 'OpenAI_Chat' && openAIChatFields">
          <div class="style-specific">
            <div v-if="openAIChatFields.messages" class="info-item">
              <span class="info-label">messages</span>
              <pre class="json-block">{{ JSON.stringify(openAIChatFields.messages, null, 2) }}</pre>
            </div>
            <div v-if="openAIChatFields.tools" class="info-item">
              <span class="info-label">tools</span>
              <pre class="json-block">{{ JSON.stringify(openAIChatFields.tools, null, 2) }}</pre>
            </div>
            <div v-if="openAIChatFields.tool_choice" class="info-item">
              <span class="info-label">tool_choice</span>
              <span class="info-value">{{ JSON.stringify(openAIChatFields.tool_choice) }}</span>
            </div>
            <div v-if="openAIChatFields.temperature !== undefined" class="info-item">
              <span class="info-label">temperature</span>
              <span class="info-value">{{ openAIChatFields.temperature }}</span>
            </div>
            <div v-if="openAIChatFields.max_tokens !== undefined" class="info-item">
              <span class="info-label">max_tokens</span>
              <span class="info-value">{{ openAIChatFields.max_tokens }}</span>
            </div>
          </div>
        </template>

        <template v-else-if="record.style === 'OpenAI_Responses' && openAIResponsesFields">
          <div class="style-specific">
            <div v-if="openAIResponsesFields.input" class="info-item">
              <span class="info-label">input</span>
              <pre class="json-block">{{ JSON.stringify(openAIResponsesFields.input, null, 2) }}</pre>
            </div>
            <div v-if="openAIResponsesFields.instructions" class="info-item">
              <span class="info-label">instructions</span>
              <span class="info-value">{{ openAIResponsesFields.instructions }}</span>
            </div>
            <div v-if="openAIResponsesFields.previous_response_id" class="info-item">
              <span class="info-label">previous_response_id</span>
              <span class="info-value">{{ openAIResponsesFields.previous_response_id }}</span>
            </div>
          </div>
        </template>

        <template v-else-if="record.style === 'Anthropic_Messages' && anthropicMessagesFields">
          <div class="style-specific">
            <div v-if="anthropicMessagesFields.model" class="info-item">
              <span class="info-label">model</span>
              <span class="info-value">{{ anthropicMessagesFields.model }}</span>
            </div>
            <div v-if="anthropicMessagesFields.messages" class="info-item">
              <span class="info-label">messages</span>
              <pre class="json-block">{{ JSON.stringify(anthropicMessagesFields.messages, null, 2) }}</pre>
            </div>
            <div v-if="anthropicMessagesFields.system" class="info-item">
              <span class="info-label">system</span>
              <span class="info-value">{{ anthropicMessagesFields.system }}</span>
            </div>
            <div v-if="anthropicMessagesFields.max_tokens" class="info-item">
              <span class="info-label">max_tokens</span>
              <span class="info-value">{{ anthropicMessagesFields.max_tokens }}</span>
            </div>
            <div v-if="anthropicMessagesFields.thinking" class="info-item">
              <span class="info-label">thinking</span>
              <pre class="json-block">{{ JSON.stringify(anthropicMessagesFields.thinking, null, 2) }}</pre>
            </div>
            <div v-if="anthropicMessagesFields.tools" class="info-item">
              <span class="info-label">tools</span>
              <pre class="json-block">{{ JSON.stringify(anthropicMessagesFields.tools, null, 2) }}</pre>
            </div>
          </div>
        </template>

        <div class="full-request">
          <span class="info-label">完整请求体</span>
          <TruncatedContent :content="requestBodyStr" />
        </div>
      </div>
    </div>

    <div v-if="record.toolCallCount > 0" class="section">
      <div class="section-header" @click="toggleSection('toolCalls')">
        <span class="section-icon">{{ toolCallsExpanded ? '▼' : '▶' }}</span>
        <span class="section-title">工具调用</span>
        <span class="section-meta">共 {{ record.toolCallCount }} 次调用</span>
      </div>
      <div v-show="toolCallsExpanded" class="section-content">
        <div class="tool-calls-list">
          <template v-if="record.requestBody?.tool_calls">
            <div v-for="(call, idx) in record.requestBody.tool_calls" :key="idx" class="tool-call-item">
              <div class="tool-call-header">
                <span class="tool-call-name">{{ call.function?.name || 'unknown' }}</span>
              </div>
              <div class="tool-call-params">
                <span class="info-label">参数</span>
                <pre class="json-block">{{ JSON.stringify(call.function?.arguments, null, 2) }}</pre>
              </div>
            </div>
          </template>
        </div>
      </div>
    </div>

    <div class="section">
      <div class="section-header" @click="toggleSection('aiReply')">
        <span class="section-icon">{{ aiReplyExpanded ? '▼' : '▶' }}</span>
        <span class="section-title">AI 回复</span>
        <span v-if="!hasAssistantText" class="section-meta">无纯文本内容</span>
        <span v-else class="section-meta">流式回放</span>
      </div>
      <div v-show="aiReplyExpanded" class="section-content">
        <StreamingText v-if="hasAssistantText" :text="assistantText" />
        <p v-else class="empty-reply">
          该记录没有可流式展示的纯文本回复（可能为工具调用或结构化响应），请查看下方「响应体」原始数据。
        </p>
      </div>
    </div>

    <div class="section">
      <div class="section-header" @click="toggleSection('response')">
        <span class="section-icon">{{ responseExpanded ? '▼' : '▶' }}</span>
        <span class="section-title">响应</span>
        <span class="section-meta status" :class="record.responseStatus >= 400 ? 'error' : 'success'">
          HTTP {{ record.responseStatus }}
        </span>
      </div>
      <div v-show="responseExpanded" class="section-content">
        <div class="response-info">
          <span class="info-label">响应状态</span>
          <span class="info-value status" :class="record.responseStatus >= 400 ? 'error' : 'success'">
            {{ record.responseStatus }}
          </span>
        </div>
        <div class="full-response">
          <span class="info-label">响应体</span>
          <TruncatedContent :content="responseBodyStr" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.chat-record-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
  height: 100%;
  overflow-y: auto;
}

.detail-title {
  font-size: 18px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 8px 0;
}

.section {
  border: 1px solid #dee2e6;
  border-radius: 8px;
  overflow: hidden;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  background: #f8f9fa;
  cursor: pointer;
  user-select: none;
}

.section-header:hover {
  background: #e9ecef;
}

.section-icon {
  font-size: 12px;
  color: #6c757d;
}

.section-title {
  font-size: 14px;
  font-weight: 600;
  color: #212529;
}

.section-meta {
  margin-left: auto;
  font-size: 12px;
  color: #6c757d;
}

.section-meta.status.error {
  color: #dc2626;
}

.section-meta.status.success {
  color: #16a34a;
}

.section-content {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 12px;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.info-label {
  font-size: 12px;
  font-weight: 500;
  color: #6c757d;
}

.info-value {
  font-size: 14px;
  color: #212529;
  word-break: break-all;
}

.info-value.status.error {
  color: #dc2626;
}

.info-value.status.success {
  color: #16a34a;
}

.json-block {
  margin: 4px 0 0 0;
  padding: 8px;
  background: #f8f9fa;
  border-radius: 4px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 12px;
  line-height: 1.4;
  color: #212529;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 200px;
  overflow-y: auto;
}

.style-specific {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
  background: #f8f9fa;
  border-radius: 6px;
}

.request-info, .response-info {
  display: flex;
  gap: 24px;
  flex-wrap: wrap;
}

.full-request, .full-response {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 8px;
}

.tool-calls-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.tool-call-item {
  padding: 12px;
  background: #f8f9fa;
  border-radius: 6px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.tool-call-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.tool-call-name {
  font-size: 14px;
  font-weight: 600;
  color: #0d6efd;
}

.tool-call-params {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.empty-reply {
  margin: 0;
  padding: 12px 14px;
  background: #f8f9fa;
  border-radius: 6px;
  font-size: 13px;
  color: #6c757d;
  line-height: 1.6;
}
</style>
