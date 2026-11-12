<script setup lang="ts">
/**
 * 消息线程右栏（PILOT-078 FR-5）。
 * in=左侧气泡 / out=右侧气泡；status=error 气泡带失败标记 + ErrorText 摘要；
 * hasMore 时顶部显示「加载更多」按钮（前插旧消息由父组件处理并保持视口位置）。
 * 滚动容器经 defineExpose 暴露：父组件轮询追加新消息后按 isNearBottom 决定是否自动滚底。
 * 样式铁律：插件是独立预编译产物，禁 <ElXxx>，一律原生 HTML + --el-* / design token（零裸色值）。
 */
import { nextTick, ref, watch } from 'vue'
import type { ChatMessageDto } from '../http'
import { bubbleTime } from './im-format'

const props = defineProps<{
  displayName: string
  messages: ChatMessageDto[]
  hasMore: boolean
  loading?: boolean
  loadingMore?: boolean
  /** 线程级错误条（请求失败保旧数据时展示）。 */
  error?: string
}>()

const emit = defineEmits<{ (e: 'load-more'): void }>()

const scrollEl = ref<HTMLElement | null>(null)

/** 是否接近底部（阈值 60px）：轮询追加新消息后据此决定是否自动滚底，不打扰向上翻阅的用户。 */
function isNearBottom(): boolean {
  const el = scrollEl.value
  if (!el) return true
  return el.scrollHeight - el.scrollTop - el.clientHeight < 60
}

/** 滚到底（rAF×2：等布局帧完成再量 scrollHeight，防 50 条大列表首帧高度未定滚不到位）。 */
function scrollToBottom(): void {
  const el = scrollEl.value
  if (!el) return
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      el.scrollTop = el.scrollHeight
    })
  })
}



defineExpose({ isNearBottom, scrollToBottom, scrollEl })

// T11（PILOT-079 R2）：滚动自治——打开/切换会话（messages 0→N 首次灌入）无条件滚到最新；
// 会话内新消息（N→M）仅在已近底部时自动滚（上翻阅读不打扰）。父组件既有调用保留为双保险（幂等）。
let hadMessages = false
watch(
  () => props.messages.length,
  async (len) => {
    if (len <= 0) {
      hadMessages = false
      return
    }
    const firstFill = !hadMessages
    hadMessages = true
    if (!firstFill && !isNearBottom()) return
    await nextTick()
    scrollToBottom()
  },
)


/** 气泡正文：error 态显示失败摘要（Content 为空串），其余显示内容原文。 */
function bubbleText(m: ChatMessageDto): string {
  if (m.status === 'error') return m.errorText || '回复失败（未知错误）'
  return m.content
}
</script>

<template>
  <div class="ig-thread">
    <div class="ig-thread__head">
      <span class="ig-thread__avatar" aria-hidden="true">{{ displayName.charAt(0).toUpperCase() }}</span>
      <span class="ig-thread__name">{{ displayName }}</span>
    </div>

    <div v-if="error" class="ig-thread__error">{{ error }}</div>

    <div ref="scrollEl" class="ig-thread__scroll">
      <div v-if="hasMore" class="ig-thread__more">
        <button type="button" class="ig-thread__more-btn" :disabled="loadingMore" @click="emit('load-more')">
          {{ loadingMore ? '加载中…' : '加载更多' }}
        </button>
      </div>

      <div v-if="loading" class="ig-thread__empty">正在加载消息…</div>
      <div v-else-if="messages.length === 0" class="ig-thread__empty">该联系人还没有消息记录</div>

      <template v-else>
        <div
          v-for="m in messages"
          :key="m.id"
          class="ig-msg"
          :class="m.direction === 'in' ? 'ig-msg--in' : 'ig-msg--out'"
        >
          <div class="ig-msg__meta">{{ bubbleTime(m.createdAt) }}<span v-if="m.direction === 'out'" class="ig-msg__tag" :class="{ 'ig-msg__tag--error': m.status === 'error' }">{{ m.status === 'error' ? '回复失败' : '回复' }}</span></div>
          <div class="ig-msg__bubble" :class="{ 'ig-msg__bubble--error': m.status === 'error' }">
            {{ bubbleText(m) }}
          </div>
        </div>
      </template>
    </div>
  </div>
</template>

<style>
/* 全 token：零裸色值 */
.ig-thread {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}
.ig-thread__head {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-bottom: 1px solid var(--el-border-color-lighter, var(--el-border-color));
  flex-shrink: 0;
}
.ig-thread__avatar {
  width: 28px;
  height: 28px;
  flex: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: color-mix(in srgb, var(--bronze-500) 20%, transparent);
  color: var(--bronze-500);
  font-weight: 600;
  font-size: 12px;
}
.ig-thread__name {
  font-family: var(--font-serif-cn, serif);
  font-size: 15px;
  font-weight: 600;
  color: var(--fg-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ig-thread__error {
  margin: 10px 14px 0;
  padding: 8px 10px;
  border-radius: var(--radius-md);
  background: color-mix(in srgb, var(--cinnabar-400) 14%, transparent);
  color: var(--cinnabar-400);
  font-size: 12px;
  flex-shrink: 0;
  word-break: break-all;
}
/* 滚动容器：子区块 flex-shrink:0（plugin-development 铁律 8） */
.ig-thread__scroll {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.ig-thread__scroll > * {
  flex-shrink: 0;
}
.ig-thread__more {
  display: flex;
  justify-content: center;
}
.ig-thread__more-btn {
  height: 28px;
  padding: 0 16px;
  border-radius: var(--radius-full, 9999px);
  border: 1px solid var(--el-border-color);
  background: transparent;
  color: var(--fg-regular, var(--fg-secondary));
  font-size: 12px;
  cursor: pointer;
}
.ig-thread__more-btn:hover:not(:disabled) {
  border-color: var(--gold-400);
  color: var(--gold-400);
}
.ig-thread__more-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.ig-thread__empty {
  margin: auto;
  text-align: center;
  font-size: 13px;
  color: var(--fg-secondary);
  padding: 30px 10px;
}
.ig-msg {
  display: flex;
  flex-direction: column;
  max-width: 78%;
}
/* 方向语义=需求拍板项 2026-10-31：回复（out）=左、来讯（in）=右；
   meta（时间/回复 tag）随 .ig-msg 的 align-items 自动跟随，配色不变 */
.ig-msg--in {
  align-self: flex-end;
  align-items: flex-end;
}
.ig-msg--out {
  align-self: flex-start;
  align-items: flex-start;
}
.ig-msg__meta {
  font-size: 11px;
  color: var(--fg-secondary);
  font-family: var(--font-mono, monospace);
  margin-bottom: 3px;
  display: flex;
  align-items: center;
  gap: 6px;
}
.ig-msg__tag {
  color: var(--gold-400);
}
.ig-msg__tag--error {
  color: var(--cinnabar-400);
}
.ig-msg__bubble {
  padding: 8px 12px;
  border-radius: var(--radius-lg, 12px);
  font-size: 13px;
  line-height: 1.6;
  color: var(--fg-primary);
  /* 双主题自适应底（--paper-white 固定米白曾在深色主题下浅字压浅泡=不可读）：
     浅=宣纸白同旧观感，深=主题深底配浅字；1px 浅边框与面板 fill 底区分 */
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter, var(--el-border-color));
  word-break: break-word;
  white-space: pre-wrap;
}
.ig-msg--out .ig-msg__bubble {
  background: color-mix(in srgb, var(--gold-400) 16%, transparent);
  border-color: color-mix(in srgb, var(--gold-400) 30%, transparent);
}
.ig-msg__bubble--error {
  background: color-mix(in srgb, var(--cinnabar-400) 10%, transparent);
  border-color: color-mix(in srgb, var(--cinnabar-400) 36%, transparent);
  color: var(--cinnabar-400);
}
</style>