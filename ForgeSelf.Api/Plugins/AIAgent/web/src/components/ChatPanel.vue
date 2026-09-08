<template>
  <section class="chat">
    <!-- 顶部条：标题 + 状态 + 模型选择 + token 计数 -->
    <header class="chat__bar">
      <div class="chat__bar-left">
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
        <!-- 模型选择器：数据来自真实接口，为空时禁用并提示 -->
        <label class="chat__model">
          <svg class="chat__model-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="4" y="4" width="16" height="16" rx="2" />
            <path d="M9 9h6v6H9z" />
          </svg>
          <select
            class="chat__model-select"
            :value="selectedModelId"
            :disabled="models.length === 0"
            @change="onModelChange"
          >
            <option v-if="models.length === 0" value="">暂无可用模型</option>
            <option v-for="m in models" :key="m.id" :value="m.chatModelId">
              {{ modelLabel(m) }}
            </option>
          </select>
        </label>

        <span class="chat__tokens">
          <svg class="chat__tokens-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M4 9h16M4 15h16M10 3L8 21M16 3l-2 18" />
          </svg>
          {{ tokenText }}
        </span>
      </div>
    </header>

    <!-- 消息列表 -->
    <div ref="listEl" class="chat__list">
      <div class="chat__list-inner">
        <!-- 无消息时的引导语（对应设计原型的 AI 欢迎消息） -->
        <div v-if="messages.length === 0" class="chat__msg">
          <div class="chat__avatar chat__avatar--ai">
            <span class="chat__avatar-dot"></span>
          </div>
          <div class="chat__bubble-wrap">
            <div class="chat__bubble chat__bubble--ai">
              你好！我是铸己匣的 AI Agent。选择模型后即可开始对话。有什么需要帮忙的吗？
            </div>
          </div>
        </div>

        <div
          v-for="msg in messages"
          :key="msg.id"
          class="chat__msg"
          :class="{ 'chat__msg--user': isUser(msg.role) }"
        >
          <!-- AI 消息：头像在左 -->
          <template v-if="!isUser(msg.role)">
            <div class="chat__avatar chat__avatar--ai">
              <span class="chat__avatar-dot"></span>
            </div>
            <div class="chat__bubble-wrap">
              <!-- 空占位（流式等待首个 token/工具事件）：显示打字指示 -->
              <div
                v-if="!msg.content && !(msg.toolEvents && msg.toolEvents.length)"
                class="chat__bubble chat__bubble--ai chat__bubble--typing"
              >…</div>
              <template v-else>
                <!-- 工具调用卡片（流式实时收集，可折叠；执行中默认展开） -->
                <div v-if="msg.toolEvents && msg.toolEvents.length" class="chat__tools">
                  <details
                    v-for="(t, ti) in msg.toolEvents"
                    :key="ti"
                    class="chat__tool-card"
                    :open="t.pending === true"
                  >
                    <summary class="chat__tool-summary">
                      <svg class="chat__tool-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M4 17l6-6-6-6M12 19h8" />
                      </svg>
                      <span class="chat__tool-name">{{ t.name }}</span>
                      <span class="chat__tool-status" :class="toolStatusClass(t)">{{ toolStatusText(t) }}</span>
                    </summary>
                    <div class="chat__tool-body">
                      <pre v-if="t.args" class="chat__tool-pre"><span class="chat__tool-label">参数</span>{{ t.args }}</pre>
                      <pre v-if="t.result" class="chat__tool-pre"><span class="chat__tool-label">结果</span>{{ t.result }}</pre>
                    </div>
                  </details>
                </div>
                <!-- 模型输出带 Markdown/LaTeX，渲染后展示。v-html 安全性由 renderMarkdown 保证：
                     先 HTML 转义、再套固定模板标签，链接仅放行 http/https/mailto（见 markdown.ts）。 -->
                <div
                  v-if="msg.content"
                  class="chat__bubble chat__bubble--ai md"
                  v-html="renderMarkdown(msg.content)"
                ></div>
              </template>
            </div>
          </template>

          <!-- 用户消息：头像在右 -->
          <template v-else>
            <div class="chat__bubble chat__bubble--user">{{ msg.content }}</div>
            <div class="chat__avatar chat__avatar--user">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
            </div>
          </template>
        </div>
      </div>
    </div>

    <!-- 错误提示 -->
    <p v-if="error" class="chat__error">{{ error }}</p>

    <!-- 输入区 -->
    <footer class="chat__input">
      <div class="chat__input-inner">
        <textarea
          v-model="draft"
          class="chat__textarea"
          rows="2"
          placeholder="输入消息…"
          :disabled="sending"
          @keydown.enter.exact.prevent="submit"
        ></textarea>
        <button
          type="button"
          class="chat__send"
          :disabled="sending || !draft.trim()"
          aria-label="发送"
          @click="submit"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z" />
          </svg>
        </button>
      </div>
    </footer>
  </section>
</template>

<script setup lang="ts">
/**
 * 中栏「完整聊天区」（对应设计原型 flex-1 主区）。
 *
 * 说明：本组件只负责展示与交互，消息的**发送与历史加载由父组件持有状态并驱动**，
 * 这样右栏才能拿到真实的消息数做统计（避免两处状态不一致）。
 */
import { computed, nextTick, ref, watch } from 'vue'
import { renderMarkdown } from '../markdown'
import type { AIModel, ChatMessage, ToolEvent } from '../types'

const props = defineProps<{
  /** 消息列表（由父组件持有）。 */
  messages: ChatMessage[]
  /** 可用模型列表。 */
  models: AIModel[]
  /** 当前选中的模型 id。 */
  selectedModelId: string
  /** 是否正在发送。 */
  sending: boolean
  /** 发送/加载的错误信息。 */
  error: string
  /** token 用量文案（后端未返回统计时由父组件传「—」）。 */
  tokenText: string
  /** 插件版本号，展示在标题后（用于确认「宿主加载的是哪个版本的插件界面」）。 */
  version: string
}>()

const emit = defineEmits<{
  /** 切换模型。 */
  (e: 'update:selectedModelId', value: string): void
  /** 发送一条消息。 */
  (e: 'send', value: string): void
}>()

/** 输入框草稿。 */
const draft = ref('')
/** 消息列表容器，用于自动滚到底。 */
const listEl = ref<HTMLElement | null>(null)

/** 判断是否为用户消息（后端角色字段大小写不固定，统一小写比较）。 */
function isUser(role: string): boolean {
  return (role ?? '').toLowerCase() === 'user'
}

/** 模型显示名：别名优先，其次上游模型 ID，最后聊天模型 ID。 */
function modelLabel(m: AIModel): string {
  return m.alias || m.upstreamModelId || m.chatModelId || '未命名模型'
}

/** 工具事件状态文案。 */
function toolStatusText(t: ToolEvent): string {
  if (t.pending) return '执行中'
  return t.success === false ? '失败' : '成功'
}

/** 工具事件状态样式类。 */
function toolStatusClass(t: ToolEvent): string {
  if (t.pending) return 'chat__tool-status--pending'
  return t.success === false ? 'chat__tool-status--fail' : 'chat__tool-status--ok'
}

function onModelChange(e: Event) {
  emit('update:selectedModelId', (e.target as HTMLSelectElement).value)
}

/** 提交消息：空内容直接忽略。 */
function submit() {
  const text = draft.value.trim()
  if (!text || props.sending) return
  emit('send', text)
  draft.value = ''
}

/** 消息变化后自动滚动到底部。除消息数/发送态外，还跟踪最后一条消息的内容长度——
 *  流式逐字期间长度持续增长，驱动滚动跟随（否则长回复流式中会停在首屏）。 */
watch(
  () => [
    props.messages.length,
    props.sending,
    props.messages[props.messages.length - 1]?.content.length ?? 0,
  ],
  async () => {
    await nextTick()
    if (listEl.value) listEl.value.scrollTop = listEl.value.scrollHeight
  }
)

/** 是否显示空态引导（无消息且未发送中）。 */
const showEmpty = computed(() => props.messages.length === 0 && !props.sending)
</script>

<style scoped>
.chat {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

/* ---- 顶部条（设计原型 44px） ---- */
.chat__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 44px;
  flex-shrink: 0;
  padding: 0 16px;
  background: var(--el-bg-color, #1d1e1f);
  border-bottom: 1px solid var(--el-border-color, #414243);
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
  color: var(--el-text-color-primary, #e5eaf3);
}

/* 版本徽标：主色淡底胶囊 */
.chat__version {
  display: inline-block;
  margin-left: 8px;
  padding: 1px 6px;
  font-size: var(--el-font-size-extra-small, 12px);
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
  border-radius: 999px;
  vertical-align: middle;
}

.chat__status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-success, #67c23a);
}

.chat__dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: var(--el-color-success, #67c23a);
}

.chat__dot--busy {
  background: var(--el-color-warning, #e6a23c);
}

/* 模型选择器：仿设计原型的描边胶囊 */
.chat__model {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-fill-color, #262727);
}

.chat__model-icon {
  width: 12px;
  height: 12px;
  color: var(--el-color-primary, #ffb84d);
}

.chat__model-select {
  max-width: 160px;
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
  color: var(--el-text-color-regular, #cfd3dc);
  background: transparent;
  border: none;
  outline: none;
  cursor: pointer;
}

.chat__tokens {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.chat__tokens-icon {
  width: 11px;
  height: 11px;
}

/* ---- 消息列表 ---- */
.chat__list {
  flex: 1;
  overflow-y: auto;
  scrollbar-width: thin;
}

.chat__list-inner {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
  max-width: 820px;
  margin: 0 auto;
  padding: 16px 24px;
}

.chat__msg {
  display: flex;
  gap: 12px;
}

.chat__msg--user {
  justify-content: flex-end;
}

/* 头像块：设计原型 32px，AI 用主色描边，用户用 accent 底 */
.chat__avatar {
  display: flex;
  align-items: flex-start;
  justify-content: center;
  width: 32px;
  height: 32px;
  flex-shrink: 0;
  border-radius: var(--el-border-radius-small, 4px);
}

.chat__avatar--ai {
  padding: 4px;
  background: var(--el-fill-color, #262727);
  border: 2px solid var(--el-color-primary, #ffb84d);
}

.chat__avatar-dot {
  width: 8px;
  height: 8px;
  margin-top: 2px;
  border-radius: 999px;
  background: var(--el-color-primary, #ffb84d);
}

.chat__avatar--user {
  padding: 6px;
  background: var(--el-color-accent-muted, rgba(96, 165, 250, 0.15));
  color: var(--el-color-accent, #60a5fa);
}

.chat__avatar--user svg {
  width: 18px;
  height: 18px;
}

.chat__bubble-wrap {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
  flex: 1;
  padding-left: 12px;
  margin-left: -2px;
  border-left: 2px solid var(--el-color-primary, #ffb84d);
}

/* 气泡：AI 与用户用不同底色，圆角左上/右上小、右下 base */
.chat__bubble {
  padding: 10px 14px;
  max-width: 85%;
  font-size: var(--el-font-size-small, 13px);
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
}

.chat__bubble--ai {
  color: var(--el-text-color-primary, #e5eaf3);
  background: var(--el-bg-color, #1d1e1f);
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-radius: 4px 4px 8px 4px;
}

.chat__msg--user .chat__bubble--user {
  color: var(--el-text-color-primary, #e5eaf3);
  background: var(--el-bg-color-overlay, #1d1e1f);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 4px 4px 8px 4px;
}

.chat__bubble--typing {
  color: var(--el-text-color-secondary, #a3a6ad);
}

/* 工具调用卡片（可折叠） */
.chat__tools {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.chat__tool-card {
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-fill-color, #262727);
  overflow: hidden;
}

.chat__tool-summary {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  cursor: pointer;
  list-style: none;
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
  color: var(--el-color-primary, #ffb84d);
  user-select: none;
}

.chat__tool-summary::-webkit-details-marker {
  display: none;
}

.chat__tool-icon {
  width: 12px;
  height: 12px;
  flex-shrink: 0;
}

.chat__tool-name {
  font-weight: var(--el-weight-medium, 500);
}

.chat__tool-status {
  margin-left: auto;
  padding: 1px 6px;
  border-radius: 999px;
}

.chat__tool-status--pending {
  color: var(--el-color-warning, #e6a23c);
  background: rgba(230, 162, 60, 0.12);
}

.chat__tool-status--ok {
  color: var(--el-color-success, #67c23a);
  background: rgba(103, 194, 58, 0.12);
}

.chat__tool-status--fail {
  color: var(--el-color-danger, #f56c6c);
  background: rgba(245, 108, 108, 0.12);
}

.chat__tool-body {
  padding: 8px 10px;
  border-top: 1px solid var(--el-border-color-dark, #2b2b2c);
}

.chat__tool-pre {
  margin: 0 0 6px;
  max-height: 200px;
  overflow-y: auto;
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
  color: var(--el-text-color-secondary, #a3a6ad);
  white-space: pre-wrap;
  word-break: break-word;
}

.chat__tool-pre:last-child {
  margin-bottom: 0;
}

.chat__tool-label {
  display: block;
  margin-bottom: 2px;
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-color-primary, #ffb84d);
}

/* ---- 错误提示 ---- */
.chat__error {
  margin: 0;
  padding: 6px 24px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-danger, #f56c6c);
  background: var(--el-bg-color, #1d1e1f);
}

/* ---- 输入区 ---- */
.chat__input {
  flex-shrink: 0;
  padding: 12px 24px;
  background: var(--el-bg-color, #1d1e1f);
  border-top: 1px solid var(--el-border-color, #414243);
}

.chat__input-inner {
  display: flex;
  align-items: flex-end;
  gap: 8px;
  width: 100%;
  max-width: 820px;
  margin: 0 auto;
}

.chat__textarea {
  flex: 1;
  min-height: 68px;
  padding: 8px 12px;
  font-size: var(--el-font-size-small, 13px);
  font-family: var(--el-font-family, inherit);
  line-height: 1.5;
  color: var(--el-text-color-primary, #e5eaf3);
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-base, 4px);
  outline: none;
  resize: none;
}

.chat__textarea:focus {
  border-color: var(--el-color-primary, #ffb84d);
}

.chat__send {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  flex-shrink: 0;
  color: var(--el-text-color-inverse, #1d1e1f);
  background: var(--el-color-primary, #ffb84d);
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  cursor: pointer;
  transition: opacity 0.15s ease;
}

.chat__send svg {
  width: 16px;
  height: 16px;
}

.chat__send:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* ---- Markdown 渲染（见 markdown.ts）---- */
/* 容器内首/末元素去掉多余外边距，气泡内排版更紧凑。 */
.md > :first-child {
  margin-top: 0;
}

.md > :last-child {
  margin-bottom: 0;
}

.md-p {
  margin: 0 0 6px;
}

.md-h {
  margin: 8px 0 4px;
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-primary, #e5eaf3);
}

.md-list {
  margin: 0 0 6px;
  padding-left: 18px;
}

.md-list li {
  margin: 2px 0;
}

.md-quote {
  margin: 4px 0;
  padding: 2px 8px;
  border-left: 3px solid var(--el-color-primary, #ffb84d);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.md-code {
  padding: 1px 4px;
  font-family: var(--el-font-family-mono, monospace);
  font-size: 0.95em;
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-fill-color, #262727);
  border-radius: 3px;
}

.md-pre {
  margin: 6px 0;
  padding: 8px 10px;
  overflow-x: auto;
  max-height: 260px;
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-radius: 4px;
}

.md-pre code {
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
  line-height: 1.5;
  color: var(--el-text-color-regular, #cfd3dc);
  white-space: pre;
}

.md-hr {
  margin: 8px 0;
  border: none;
  border-top: 1px solid var(--el-border-color, #414243);
}

.md-link {
  color: var(--el-color-primary, #ffb84d);
  text-decoration: underline;
}
</style>
