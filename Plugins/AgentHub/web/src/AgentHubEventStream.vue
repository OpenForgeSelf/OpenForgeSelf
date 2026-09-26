<script setup lang="ts">
/**
 * 任务事件流视图：把后端归一化事件渲染成可读的时间线。
 *
 * 事件词表（与后端 AgentEventTypes 一一对应，新增类型必须在这里加分支，
 * 否则会掉到 default 兜底——不是崩溃，但用户看不懂裸 JSON）：
 *   Text / Thought / ToolCall / ToolResult / FileChange / Diff / PermissionRequest / Error / Exit / Meta
 */

import { computed, ref } from 'vue'
import type { TaskEventDto } from './http'

const props = defineProps<{
  events: TaskEventDto[]
}>()

/** 折叠开关：默认只展开「有内容」的事件类型，噪音类型（Meta）默认收起。 */
const showMeta = ref(false)

const visibleEvents = computed(() =>
  showMeta.value ? props.events : props.events.filter((e) => e.type !== 'Meta'),
)

const hiddenCount = computed(() => props.events.length - visibleEvents.value.length)

/** 事件类型 → 中文标签 + 样式后缀。 */
function typeMeta(type: string): { label: string; cls: string } {
  switch (type) {
    case 'Text':
      return { label: '输出', cls: 'text' }
    case 'Thought':
      return { label: '思考', cls: 'thought' }
    case 'ToolCall':
      return { label: '工具调用', cls: 'tool' }
    case 'ToolResult':
      return { label: '工具结果', cls: 'tool' }
    case 'FileChange':
      return { label: '文件变更', cls: 'file' }
    case 'Diff':
      return { label: '差异', cls: 'file' }
    case 'PermissionRequest':
      return { label: '权限申请', cls: 'perm' }
    case 'Error':
      return { label: '错误', cls: 'error' }
    case 'Exit':
      return { label: '进程退出', cls: 'exit' }
    case 'Meta':
      return { label: '元信息', cls: 'meta' }
    default:
      return { label: type, cls: 'meta' }
  }
}

/** 从 payload JSON 中尽力抽取可读文本（payload 是上游原始片段，结构不保证统一）。 */
function payloadText(evt: TaskEventDto): string {
  if (!evt.payload) return ''
  try {
    const obj = JSON.parse(evt.payload) as Record<string, unknown>
    // 常见的几个载荷字段，按优先级取；都没有就原样展示 JSON
    for (const k of ['text', 'message', 'content', 'detail', 'command', 'path', 'name']) {
      const v = obj[k]
      if (typeof v === 'string' && v) return v
    }
    return evt.payload
  } catch {
    // 非 JSON 载荷（如纯文本片段）原样返回
    return evt.payload
  }
}

function timeText(ts?: string): string {
  if (!ts) return ''
  const d = new Date(ts)
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleTimeString('zh-CN', { hour12: false })
}
</script>

<template>
  <div class="ok-stream">
    <div class="ok-stream__head">
      <span class="ok-stream__title">事件流（{{ events.length }}）</span>
      <label v-if="hiddenCount >= 0" class="ok-stream__toggle">
        <input type="checkbox" v-model="showMeta" />
        <span>显示元信息{{ hiddenCount > 0 && !showMeta ? `（已折叠 ${hiddenCount} 条）` : '' }}</span>
      </label>
    </div>

    <div v-if="!visibleEvents.length" class="ok-stream__empty">暂无事件。</div>

    <ul v-else class="ok-stream__list">
      <li v-for="e in visibleEvents" :key="e.seq" class="ok-evt" :class="'ok-evt--' + typeMeta(e.type).cls">
        <span class="ok-evt__seq">{{ e.seq }}</span>
        <span class="ok-evt__type">{{ typeMeta(e.type).label }}</span>
        <span class="ok-evt__body">
          <template v-if="e.type === 'Exit'">
            退出码 {{ (() => { try { return JSON.parse(e.payload ?? '{}').exitCode ?? '—' } catch { return '—' } })() }}
          </template>
          <template v-else>{{ payloadText(e) || '（无内容）' }}</template>
          <span v-if="e.truncated" class="ok-evt__trunc">内容已截断</span>
        </span>
        <span class="ok-evt__time">{{ timeText(e.timestamp) }}</span>
      </li>
    </ul>
  </div>
</template>

<style>
.ok-stream {
  margin-top: 4px;
}
.ok-stream__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.ok-stream__title {
  font-size: 13px;
  font-weight: 600;
}
.ok-stream__toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
}
.ok-stream__empty {
  padding: 20px;
  text-align: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.ok-stream__list {
  list-style: none;
  margin: 0;
  padding: 0;
  max-height: 420px;
  overflow-y: auto;
  font-size: 12px;
}
.ok-evt {
  display: grid;
  grid-template-columns: 34px 68px 1fr 62px;
  gap: 8px;
  align-items: baseline;
  padding: 5px 8px;
  border-left: 2px solid var(--el-border-color-lighter, #ebeef5);
  margin-bottom: 2px;
  border-radius: 0 var(--el-border-radius-base, 6px) var(--el-border-radius-base, 6px) 0;
}
.ok-evt__seq {
  color: var(--el-text-color-placeholder, #a8abb2);
  font-family: var(--el-font-family-mono, monospace);
  text-align: right;
}
.ok-evt__type {
  font-weight: 500;
  white-space: nowrap;
}
.ok-evt__body {
  line-height: 1.6;
  word-break: break-word;
  white-space: pre-wrap;
  color: var(--el-text-color-regular);
}
.ok-evt__time {
  color: var(--el-text-color-placeholder, #a8abb2);
  font-family: var(--el-font-family-mono, monospace);
  text-align: right;
}
.ok-evt__trunc {
  margin-left: 6px;
  font-size: 11px;
  color: var(--el-color-warning, #e6a23c);
}
/* 按事件语义着色（全部走 --el-* token） */
.ok-evt--text {
  border-left-color: var(--el-color-primary, #409eff);
}
.ok-evt--text .ok-evt__type {
  color: var(--el-color-primary, #409eff);
}
.ok-evt--thought {
  border-left-color: var(--el-text-color-placeholder, #a8abb2);
}
.ok-evt--thought .ok-evt__body {
  color: var(--el-text-color-secondary);
  font-style: italic;
}
.ok-evt--tool {
  border-left-color: var(--el-color-primary-light-3, #79bbff);
}
.ok-evt--tool .ok-evt__type {
  color: var(--el-color-primary-light-3, #79bbff);
}
.ok-evt--file {
  border-left-color: var(--el-color-success, #67c23a);
}
.ok-evt--file .ok-evt__type {
  color: var(--el-color-success, #67c23a);
}
.ok-evt--perm {
  border-left-color: var(--el-color-warning, #e6a23c);
  background: var(--el-color-warning-light-9, #fdf6ec);
}
.ok-evt--perm .ok-evt__type {
  color: var(--el-color-warning, #e6a23c);
}
.ok-evt--error {
  border-left-color: var(--el-color-danger, #f56c6c);
  background: var(--el-color-danger-light-9, #fef0f0);
}
.ok-evt--error .ok-evt__type {
  color: var(--el-color-danger, #f56c6c);
}
.ok-evt--exit {
  border-left-color: var(--el-text-color-secondary);
  font-weight: 500;
}
.ok-evt--meta .ok-evt__body {
  color: var(--el-text-color-placeholder, #a8abb2);
}
</style>
